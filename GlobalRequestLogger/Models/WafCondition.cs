namespace GlobalRequestLogger.Models
{
    internal class WafCondition
    {
        internal int Id { get; set; }
        internal string Campo { get; set; } // Field to evaluate (e.g., URL, IP, User-Agent)
        internal string Operador { get; set; } // Operator (e.g., Exact, StartsWith, Contains, Regex)
        internal string Valor { get; set; } // Value to match against
        internal string Logica { get; set; } // Logical operator (AND/OR)
    }
}