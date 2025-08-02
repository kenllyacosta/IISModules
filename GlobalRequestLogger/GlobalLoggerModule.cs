using GlobalRequestLogger.Models;
using GlobalRequestLogger.Services;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace GlobalRequestLogger
{
    public class GlobalLoggerModule : IHttpModule
    {
        private const string TokenKey = "asl_clearance";
        private static readonly TimeSpan TokenExpirationDuration = TimeSpan.FromHours(11);
        private static readonly string _connectionString = "Server=.;Database=GlobalRequests;Integrated Security=True;TrustServerCertificate=True;";

        public void Init(HttpApplication context)
        {
            context.BeginRequest += Context_BeginRequest;
            context.EndRequest += Context_EndRequest;
            context.PreSendRequestHeaders += Context_PreSendRequestHeaders;
        }

        private static void Context_BeginRequest(object sender, EventArgs e)
        {
            var app = (HttpApplication)sender;
            var request = app.Context.Request;
            var response = app.Context.Response;
            RequestLogger.Enqueue(request, _connectionString, "traffic");

            // Fetch rules for the application
            var rules = FetchWafRules(request.Url.Host);

            foreach (var rule in rules.OrderBy(r => r.Prioridad))
            {
                if (!rule.Habilitado) 
                    continue;

                // Evaluate conditions
                bool isMatch = EvaluateConditions(rule.Conditions, request);
                if (isMatch)
                {
                    // Check if the user has a valid token
                    var token = request.Cookies[TokenKey]?.Value;
                    string key = "4829103746582931";

                    // Take action based on the rule
                    //skip, block, Managed Challenge, Interactive Challenge, Log
                    switch (rule.Accion.ToLower())
                    {
                        case "skip"://Let the request proceed
                            RequestLogger.Enqueue(request, _connectionString, "skip");
                            return;
                        case "block": //Block the request
                            RequestLogger.Enqueue(request, _connectionString, "block");
                            response.StatusCode = 403;
                            response.ContentType = "text/html";
                            response.Write(GenerateHTMLUserBlockedPage(request.Url.Host, Guid.NewGuid().ToString()));
                            response.End();
                            return;
                        case "managed challenge": // Render a managed challenge page
                            if (string.IsNullOrEmpty(token) || !IsTokenValid(token)) 
                            {
                                RequestLogger.Enqueue(request, _connectionString, "managed challenge");
                                // Generate and render the token generation page on the fly
                                response.ContentType = "text/html";
                                response.Write(GenerateHTMLManagedChallenge(request.Url.Host, Guid.NewGuid().ToString()));

                                // If the request is a POST, generate the token
                                if (request.HttpMethod == "POST")
                                {
                                    var newToken = RequestLogger.Encrypt(Guid.NewGuid().ToString(), key);
                                    var expirationTime = DateTime.UtcNow.Add(TokenExpirationDuration);
                                    HttpContext.Current.Application[newToken] = expirationTime;

                                    // Set the cookie with the new token
                                    response.Cookies.Add(new HttpCookie(TokenKey, newToken) { Expires = expirationTime, HttpOnly = true, SameSite = SameSiteMode.Strict });

                                    // Redirect only if the token is not already valid
                                    if (IsTokenValid(newToken))
                                    {
                                        // Avoid triggering BeginRequest again
                                        response.Redirect(request.Url.AbsolutePath, false);

                                        // End the request properly
                                        HttpContext.Current.ApplicationInstance.CompleteRequest();
                                    }
                                }
                            }
                            return;
                        case "interactive challenge": // Render an interactive challenge page
                            if (string.IsNullOrEmpty(token) || !IsTokenValid(token))
                            {
                                RequestLogger.Enqueue(request, _connectionString, "interactive challenge");
                                // Generate and render the token generation page on the fly
                                response.ContentType = "text/html";
                                response.Write(GenerateHTMLInteractiveChallenge(request.Url.Host, Guid.NewGuid().ToString()));

                                // If the request is a POST, generate the token
                                if (request.HttpMethod == "POST")
                                {
                                    var newToken = RequestLogger.Encrypt(Guid.NewGuid().ToString(), key);
                                    var expirationTime = DateTime.UtcNow.Add(TokenExpirationDuration);
                                    HttpContext.Current.Application[newToken] = expirationTime;

                                    // Set the cookie with the new token
                                    response.Cookies.Add(new HttpCookie(TokenKey, newToken) { Expires = expirationTime, HttpOnly = true, SameSite = SameSiteMode.Strict });

                                    // Redirect only if the token is not already valid
                                    if (IsTokenValid(newToken))
                                    {
                                        // Avoid triggering BeginRequest again
                                        response.Redirect(request.Url.AbsolutePath, false);

                                        // End the request properly
                                        HttpContext.Current.ApplicationInstance.CompleteRequest();
                                    }
                                }
                            }
                            return;
                        case "log": // Log the request
                            RequestLogger.Enqueue(request, _connectionString, "log");
                            return;
                    }
                }
            }

            // Start timing the request
            app.Context.Items["RequestStartTime"] = Stopwatch.StartNew();
        }

        private static IEnumerable<WafRule> FetchWafRules(string host)
        {
            // Query the database to fetch rules for the given host
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var command = new SqlCommand(@"
                SELECT r.Id, r.Nombre, r.Accion, r.Prioridad, r.Habilitado
                FROM WafRuleEntity r
                INNER JOIN AppEntity a ON r.AppId = a.Id
                WHERE a.Host = @Host AND r.Habilitado = 1", connection);
                command.Parameters.AddWithValue("@Host", host);

                var reader = command.ExecuteReader();
                var rules = new List<WafRule>();
                while (reader.Read())
                {
                    rules.Add(new WafRule
                    {
                        Id = reader.GetInt32(0),
                        Nombre = reader.GetString(1),
                        Accion = reader.GetString(2),
                        Prioridad = reader.GetInt32(3),
                        Habilitado = reader.GetBoolean(4),
                        Conditions = FetchWafConditions(reader.GetInt32(0))
                    });
                }
                return rules;
            }
        }

        private static List<WafCondition> FetchWafConditions(int ruleId)
        {
            // Query the database to fetch conditions for the given ruleId
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var command = new SqlCommand(@"
                SELECT Campo, Operador, Valor, Logica, Id
                FROM WafConditionEntity
                WHERE WafRuleEntityId = @RuleId", connection);
                command.Parameters.AddWithValue("@RuleId", ruleId);

                var reader = command.ExecuteReader();
                var conditions = new List<WafCondition>();
                while (reader.Read())
                {
                    conditions.Add(new WafCondition
                    {
                        Campo = reader.GetString(0),
                        Operador = reader.GetString(1),
                        Valor = reader.GetString(2),
                        Logica = reader.GetString(3), 
                        Id = reader.GetInt32(4)
                    });
                }
                return conditions;
            }
        }

        private static bool EvaluateConditions(IEnumerable<WafCondition> conditions, HttpRequest request)
        {
            // Evaluate conditions against the request
            bool result = true;
            foreach (var condition in conditions)
            {
                bool match = EvaluateCondition(condition, request);
                if (condition.Logica == "AND" && !match) 
                    return false;

                if (condition.Logica == "OR" && match) 
                    return true;
            }
            return result;
        }

        private static bool EvaluateCondition(WafCondition condition, HttpRequest request)
        {
            // Evaluate a single condition
            var fieldValue = GetFieldValue(condition.Campo, request);
            switch (condition.Operador.ToLower())
            {
                case "equals":
                    return fieldValue == condition.Valor;
                case "does not equals":
                    return fieldValue != condition.Valor;
                case "contains":
                    return fieldValue.Contains(condition.Valor);
                case "does not contain":
                    return !fieldValue.Contains(condition.Valor);
                case "matches regex":
                    return Regex.IsMatch(fieldValue, condition.Valor);
                case "does not match":
                    return !Regex.IsMatch(fieldValue, condition.Valor);
                case "starts with":
                    return fieldValue.StartsWith(condition.Valor, StringComparison.OrdinalIgnoreCase);
                case "does not start with":
                    return !fieldValue.StartsWith(condition.Valor, StringComparison.OrdinalIgnoreCase);
                case "ends with":
                    return fieldValue.EndsWith(condition.Valor, StringComparison.OrdinalIgnoreCase);
                case "does not end with":
                    return !fieldValue.EndsWith(condition.Valor, StringComparison.OrdinalIgnoreCase);
                default:
                    return false;
            }
        }

        private static string GetFieldValue(string field, HttpRequest request)
        {
            // Extract the value of the specified field from the request
            switch (field.ToLower())
            {
                case "cookie":
                    return string.Join(",", request.Cookies.AllKeys.Select(x => request.Cookies[x].Values.ToString().Replace("+", " ")));
                case "hostname":
                    return request.Url.Host;
                case "ip":
                    return request.UserHostAddress;
                case "referrer":
                    return request.UrlReferrer?.AbsoluteUri ?? string.Empty;
                case "method":
                    return request.HttpMethod;
                case "httpversion":
                    return HttpContext.Current.Request.ServerVariables["SERVER_PROTOCOL"];
                case "user-agent":
                    return request.UserAgent;
                case "x-forwarded-for":
                    return request.Headers["X-Forwarded-For"];
                case "mimetype":
                    return request.ContentType;
                case "url-full":
                    return request.Url.Host + request.Url.AbsolutePath;
                case "url":
                    return request.Url.AbsolutePath;
                case "url-path":
                    return request.Url.PathAndQuery;
                case "url-querystring":
                    return request.QueryString.ToString();
                default:
                    return string.Empty;
            }
        }

        private static string GenerateHTMLInteractiveChallenge(string rootDomain, string rayId)
            => $@"<!DOCTYPE html>
                <html>
                <head>
                    <title>Just a moment...</title>
                    <style>
                        body {{
                            display: flex;
                            justify-content: center;
                            align-items: center;
                            height: 100vh;
                            margin: 0;
                            font-family: Arial, sans-serif;
                            background-color: #f9f9f9;
                        }}
                        .container {{
                            text-align: center;
                            padding: 20px;
                            border: 1px solid #ccc;
                            border-radius: 8px;
                            background-color: #fff;
                            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
                            display: none; /* Hide container by default */
                        }}
                        .checkbox-container {{
                            display: flex;
                            align-items: center;
                            justify-content: center;
                            margin-top: 20px;
                            animation: fadeIn 1s ease;
                        }}
                        input[type='checkbox'] {{
                            margin-right: 10px;
                            transform: scale(1.5);
                            transition: transform 0.3s ease;
                        }}
                        input[type='checkbox']:hover {{
                            transform: scale(1.7);
                        }}
                        input[type='checkbox'].clicked {{
                            animation: pulse 0.5s ease;
                        }}
                        .form-processing {{
                            animation: fadeOut 1s ease forwards;
                        }}
                        .loader {{
                            display: none;
                            border: 4px solid #f3f3f3;
                            border-top: 4px solid #007bff;
                            border-radius: 50%;
                            width: 40px;
                            height: 40px;
                            animation: spin 1s linear infinite;
                            margin: 20px auto;
                        }}
                        .loader.active {{
                            display: block;
                        }}
                        .noscript-message, .nocookies-message {{
                            color: red;
                            font-size: 16px;
                            margin-top: 20px;
                        }}
                        .checkbox-box {{
                            display: flex;
                            align-items: center;
                            justify-content: center;
                            padding: 15px;
                            border: 2px solid #ccc;
                            border-radius: 8px;
                            background-color: #f9f9f9;
                            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
                            transition: box-shadow 0.3s ease, border-color 0.3s ease;
                        }}
                        .checkbox-box:hover {{
                            box-shadow: 0 6px 8px rgba(0, 0, 0, 0.15);
                            border-color: #007bff;
                        }}
                        .checkbox-box input[type='checkbox'] {{
                            margin-right: 10px;
                            transform: scale(1.5);
                            transition: transform 0.3s ease;
                        }}
                        .checkbox-box input[type='checkbox']:hover {{
                            transform: scale(1.7);
                        }}
                        .checkbox-box label {{
                            font-size: 16px;
                            font-weight: bold;
                            color: #333;
                        }}
                        @keyframes fadeIn {{
                            from {{ opacity: 0; transform: translateY(-20px); }}
                            to {{ opacity: 1; transform: translateY(0); }}
                        }}
                        @keyframes fadeOut {{
                            from {{ opacity: 1; transform: translateY(0); }}
                            to {{ opacity: 0; transform: translateY(-20px); }}
                        }}
                        @keyframes pulse {{
                            0% {{ transform: scale(1.5); }}
                            50% {{ transform: scale(1.8); }}
                            100% {{ transform: scale(1.5); }}
                        }}
                        @keyframes spin {{
                            0% {{ transform: rotate(0deg); }}
                            100% {{ transform: rotate(360deg); }}
                        }}
                    </style>
                    <script>
                        // Check if cookies are enabled
                        function checkCookies() {{
                            document.cookie = 'sitecookie=1';
                            const cookiesEnabled = document.cookie.indexOf('sitecookie=') !== -1;
                            if (!cookiesEnabled) {{
                                const cookieMessage = document.querySelector('.nocookies-message');
                                cookieMessage.style.display = 'block';
                                return false;
                            }}
                            return true;
                        }}

                        // Show the container if JavaScript and cookies are enabled
                        function showContainerIfEnabled() {{
                            const container = document.querySelector('.container');
                            if (checkCookies()) {{
                                container.style.display = 'block';
                            }}
                        }}

                        // Show loader and hide checkbox-container for 3 seconds on page load
                        function showLoaderOnPageLoad() {{
                            const loader = document.querySelector('.loader');
                            const checkboxContainer = document.querySelector('.checkbox-container');
                            const checkbox = document.querySelector('#verifyCheckbox');

                            checkbox.disabled = true; // Disable the checkbox
                            checkboxContainer.style.display = 'none'; // Hide the checkbox-container
                            loader.classList.add('active'); // Show the loader

                            setTimeout(() => {{
                                loader.classList.remove('active'); // Hide the loader
                                checkboxContainer.style.display = 'flex'; // Show the checkbox-container
                                checkbox.disabled = false; // Re-enable the checkbox
                            }}, 3000);
                        }}

                        function handleCheckboxChange(event) {{
                            const checkbox = event.target;
                            const form = checkbox.closest('form');
                            const loader = document.querySelector('.loader');
                            const checkboxContainer = document.querySelector('.checkbox-container');

                            // If the checkbox is already disabled, return early to prevent duplicate actions
                            if (checkbox.disabled) {{
                                return;
                            }}

                            // Disable the checkbox to prevent multiple triggers
                            checkbox.disabled = true;

                            // Add animation class to checkbox
                            checkbox.classList.add('clicked');

                            // Remove the animation class after it completes
                            setTimeout(() => {{
                                checkbox.classList.remove('clicked');
                            }}, 500);

                            // If the checkbox is checked, show the loader and process the form
                            if (checkbox.checked) {{
                                loader.classList.add('active');
                                checkboxContainer.style.display = 'none'; // Hide the checkbox-container
                                setTimeout(() => {{
                                    form.submit(); // Submit the form after the loader animation
                                }}, 3000); // Wait for the loader to complete
                            }}
                        }}

                        // Attach event listener dynamically
                        window.onload = () => {{
                            showContainerIfEnabled();
                            showLoaderOnPageLoad();

                            const checkbox = document.querySelector('#verifyCheckbox');
                            checkbox.addEventListener('change', handleCheckboxChange);
            
                            console.log(""%c ¡Espera!"", ""color: Red; font-size: 45px; font-weight: bold;"");
                            console.log(""%cEsta función del navegador está pensada para desarrolladores. Si alguien te indicó que copiaras y pegaras algo aquí para habilitar una función o para \""piratear\"" la cuenta de alguien, se trata de un fraude."", ""color: green; font-size: x-large;"");
                        }};
                    </script>
                </head>
                <body>
                    <div class=""container"">
                        <h1>{rootDomain}</h1>
                        <b><p>Verificando que eres humano. Esto puede durar unos segundos.</p></b>
                        <form method=""post"" action="""">
                            <div class=""checkbox-container"">
                                <div class=""checkbox-box"">
                                    <input type=""checkbox"" id=""verifyCheckbox"">
                                    <label for=""verifyCheckbox"">No soy un robot</label>
                                </div>
                            </div>
                            <div class=""loader""></div>
                        </form>
                        <br/>
                        <p>{rootDomain} necesita revisar la seguridad de la conexión antes de proceder.</p>
                        <br/><br/><br/>
                        <hr/>
                        <em>Ray Id: {rayId}</em>
                        <br/>   
                        <em>Powered by {rootDomain}</em>
                    </div>
                    <noscript>
                        <div class=""noscript-message"">
                            JavaScript is disabled in your browser. Please enable JavaScript to proceed.
                        </div>
                    </noscript>
                </body>
                </html>";

        private static string GenerateHTMLManagedChallenge(string rootDomain, string rayId)
            => $@"<!DOCTYPE html>
        <html>
        <head>
            <title>Just a moment...</title>
            <style>
                body {{
                    display: flex;
                    justify-content: center;
                    align-items: center;
                    height: 100vh;
                    margin: 0;
                    font-family: Arial, sans-serif;
                    background-color: #f9f9f9;
                }}
                .container {{
                    text-align: center;
                    padding: 20px;
                    border: 1px solid #ccc;
                    border-radius: 8px;
                    background-color: #fff;
                    box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
                    display: none; /* Hide container by default */
                }}
                .loader {{
                    display: none;
                    border: 4px solid #f3f3f3;
                    border-top: 4px solid #007bff;
                    border-radius: 50%;
                    width: 40px;
                    height: 40px;
                    animation: spin 1s linear infinite;
                    margin: 20px auto;
                }}
                .loader.active {{
                    display: block;
                }}
                .noscript-message, .nocookies-message {{
                    color: red;
                    font-size: 16px;
                    margin-top: 20px;
                }}
                @keyframes spin {{
                    0% {{ transform: rotate(0deg); }}
                    100% {{ transform: rotate(360deg); }}
                }}
            </style>
            <script>
                function checkCookies() {{
                    document.cookie = 'sitecookie=1';
                    const cookiesEnabled = document.cookie.indexOf('sitecookie=') !== -1;
                    if (!cookiesEnabled) {{
                        const cookieMessage = document.querySelector('.nocookies-message');
                        cookieMessage.style.display = 'block';
                        return false;
                    }}
                    return true;
                }}

                function showContainerIfEnabled() {{
                    const container = document.querySelector('.container');
                    if (checkCookies()) {{
                        container.style.display = 'block';
                    }}
                }}

                function showLoaderOnPageLoad() {{
                    const loader = document.querySelector('.loader');
                    const form = document.querySelector('form');

                    loader.classList.add('active'); // Show the loader

                    setTimeout(() => {{
                        loader.classList.remove('active'); // Hide the loader
                        form.submit(); // Automatically submit the form
                    }}, 3000);
                }}

                window.onload = () => {{
                    showContainerIfEnabled();
                    showLoaderOnPageLoad();
                }};
            </script>
        </head>
        <body>
            <div class=""container"">
                <h1>{rootDomain}</h1>
                <b><p>Verificando que eres humano. Esto puede durar unos segundos.</p></b>
                <form method=""post"" action="""">
                    <div class=""loader""></div>
                </form>
                <br/>
                <p>{rootDomain} necesita revisar la seguridad de la conexión antes de proceder.</p>
                <br/><br/><br/>
                <hr/>
                <em>Ray Id: {rayId}</em>
                <br/>   
                <em>Powered by {rootDomain}</em>
            </div>
            <noscript>
                <div class=""noscript-message"">
                    JavaScript is disabled in your browser. Please enable JavaScript to proceed.
                </div>
            </noscript>
        </body>
        </html>";

        private static string GenerateHTMLUserBlockedPage(string rootDomain, string rayId)
            => $@"<!DOCTYPE html>
        <html>
        <head>
            <title>Access Denied</title>
            <style>
                body {{
                    display: flex;
                    justify-content: center;
                    align-items: center;
                    height: 100vh;
                    margin: 0;
                    font-family: Arial, sans-serif;
                    background-color: #f9f9f9;
                }}
                .container {{
                    text-align: center;
                    padding: 20px;
                    border: 1px solid #ccc;
                    border-radius: 8px;
                    background-color: #fff;
                    box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
                }}
                h1 {{
                    color: #d9534f;
                }}
                p {{
                    font-size: 16px;
                    color: #333;
                }}
                .details {{
                    margin-top: 20px;
                    font-size: 14px;
                    color: #777;
                }}
                hr {{
                    margin: 20px 0;
                    border: none;
                    border-top: 1px solid #ddd;
                }}
            </style>
        </head>
        <body>
            <div class=""container"">
                <h1>Access Denied</h1>
                <p>Your request has been blocked by the server's security rules.</p>
                <p>If you believe this is an error, please contact the website administrator.</p>
                <div class=""details"">
                    <hr/>
                    <p><strong>Domain:</strong> {rootDomain}</p>
                    <p><strong>Ray ID:</strong> {rayId}</p>
                    <hr/>
                </div>
            </div>
        </body>
        </html>";

        private static bool IsTokenValid(string token)
        {
            // Validate the token (e.g., check expiration)
            if (HttpContext.Current.Application[token] is DateTime expirationTime)
                return DateTime.UtcNow <= expirationTime;

            return false;
        }

        private static void Context_EndRequest(object sender, EventArgs e)
        {
            var app = (HttpApplication)sender;

            if (app.Context.Items["RequestStartTime"] is Stopwatch stopwatch)
            {
                stopwatch.Stop();

                // Fire-and-forget logging
                RequestLogger.EnqueueResponse(app.Context.Request.Url?.ToString(), app.Context.Request.HttpMethod, stopwatch.ElapsedMilliseconds, DateTime.UtcNow, _connectionString);
            }
        }

        private static void Context_PreSendRequestHeaders(object sender, EventArgs e)
        {
            var app = (HttpApplication)sender;
            var response = app.Context.Response;

            // Remove unnecessary headers for security
            response.Headers.Remove("X-Powered-By");
            response.Headers.Remove("Server");
            response.Headers.Remove("X-AspNet-Version");
            response.Headers.Remove("X-AspNetMvc-Version");
        }

        public void Dispose() { }
    }
}