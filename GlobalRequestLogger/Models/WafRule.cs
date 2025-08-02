using System.Collections.Generic;

namespace GlobalRequestLogger.Models
{
    internal class WafRule
    {
        internal int Id { get; set; }
        internal string Nombre { get; set; } // Rule name
        internal string Accion { get; set; } // Action (e.g., skip, block, Managed Challenge, Interactive Challenge, Log)
        internal int Prioridad { get; set; } // Priority
        internal bool Habilitado { get; set; } // Enabled status
        internal IEnumerable<WafCondition> Conditions { get; set; } // Associated conditions

        public WafRule()
        {
            Conditions = new List<WafCondition>();
        }
    }
}