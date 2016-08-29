using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OJS.Workers.ExecutionStrategies
{
    public class MochaTestResult
    {
        public Dictionary<string, dynamic> stats { get; set; }

        public Dictionary<string, dynamic>[] tests { get; set; }

        public Dictionary<string, dynamic>[] pending { get; set; }

        public Dictionary<string, dynamic>[] failures { get; set; }

        public Dictionary<string, dynamic>[] passes { get; set; }

        public override string ToString()
        {
            var r = new StringBuilder();
            r.AppendLine("-----------------------");
            r.AppendLine("Passed: ");
            if (this.passes != null && this.passes.Any())
            {
                foreach (var pair in this.passes.First())
                {
                    r.AppendLine($"{pair.Key}: {pair.Value}");
                }
            }
            else
            {
                r.AppendLine("None");
            }

            r.AppendLine("-----------------------");
            r.AppendLine("Failed: ");
            if (this.failures != null && this.failures.Any())
            {
                foreach (var pair in this.failures.First())
                {
                    r.AppendLine($"{pair.Key}: {pair.Value}");
                }
            }
            else
            {
                r.AppendLine("None");
            }
            return r.ToString();
        }
    }
}
