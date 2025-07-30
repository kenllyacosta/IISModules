using GlobalRequestLogger.Services;
using System;
using System.Diagnostics;
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

            // Fire-and-forget logging
            RequestLogger.Enqueue(request, _connectionString);
            var response = app.Context.Response;

            // Check if the user has a valid token
            var token = request.Cookies[TokenKey]?.Value;
            if (string.IsNullOrEmpty(token) || !IsTokenValid(token))
            {
                // Generate and render the token generation page on the fly
                response.ContentType = "text/html";
                response.Write(GenerateHTMLResponse(request.Url.Host, Guid.NewGuid().ToString()));

                // If the request is a POST, generate the token
                if (request.HttpMethod == "POST")
                {
                    var newToken = RequestLogger.Encrypt(Guid.NewGuid().ToString(), "4829103746582931");
                    var expirationTime = DateTime.UtcNow.Add(TokenExpirationDuration);
                    HttpContext.Current.Application[newToken] = expirationTime;

                    // Set the cookie with the new token
                    response.Cookies.Add(new HttpCookie(TokenKey, newToken)
                    {
                        Expires = expirationTime,
                        HttpOnly = true,
                        SameSite = SameSiteMode.Strict
                    });

                    // Redirect only if the token is not already valid
                    if (IsTokenValid(newToken))
                    {
                        // Avoid triggering BeginRequest again
                        response.Redirect(request.Url.AbsolutePath, false); 

                        // End the request properly
                        HttpContext.Current.ApplicationInstance.CompleteRequest(); 
                    }
                }

                response.End();
                return;
            }

            // Start timing the request
            app.Context.Items["RequestStartTime"] = Stopwatch.StartNew();
        }

        private static string GenerateHTMLResponse(string rootDomain, string rayId)
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
                </html>".Replace("@DomainName", rootDomain).Replace("@RayId", rayId);

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