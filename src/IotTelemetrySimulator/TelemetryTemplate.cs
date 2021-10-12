namespace IotTelemetrySimulator
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    // A function that represents a part of the template.
    // It accepts variable values and returns this part of the template with the variables substituted.
    using TemplateToken = System.Func<System.Collections.Generic.Dictionary<string, object>, string>;

    public class TelemetryTemplate
    {
        private readonly string template;

        // Internal representation of the template that allows fast variable substitution.
        private List<TemplateToken> templateTokens;

        /// <summary>
        /// Initializes a new instance of the <see cref="TelemetryTemplate"/> class.
        /// <param name="variableNames">All possible variable names that will be substituted
        /// in this template in the future.</param>
        /// </summary>
        public TelemetryTemplate(string template, IEnumerable<string> variableNames)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                throw new ArgumentException("Invalid template", nameof(template));
            }

            this.template = template;
            this.TokenizeTemplate(variableNames);
        }

        /// <summary>
        /// Replace all variable names in the template with the given variable values.
        /// </summary>
        public string Create(Dictionary<string, object> variables)
        {
            return string.Join(
                string.Empty,
                this.templateTokens.Select(token => token(variables)));
        }

        internal string GetTemplateDefinition()
        {
            return this.template;
        }

        public override string ToString()
        {
            return this.template;
        }

        /// <summary>
        /// Preprocess the template and generate a list of tokens that will allow
        /// fast variable substitution in the template in <see cref="Create"/>.
        ///
        /// Preprocessing itself has O(len(template) * len(variables)) complexity,
        /// and it will take ~seconds on large templates with many variables.
        /// It is OK because this class is created once on service startup:
        /// we make startup slightly longer, but in the runtime it's fast.
        ///
        /// The template "Hello, $.name! I like $.var1, $.var11 and $.var12, $.name."
        /// will be represented as a list of functions:
        /// [
        ///     (vars) => "Hello, ",
        ///     (vars) => vars.Contains("name") ? vars["name"].ToString() : "$.name",
        ///     (vars) => "! I like ",
        ///     (vars) => vars.Contains("var1") ? vars["var1"].ToString() : "$.var1",
        ///     (vars) => ", ",
        ///     (vars) => vars.Contains("var11") ? vars["var11"].ToString() : "$.var11",
        ///     (vars) => " and ",
        ///     (vars) => vars.Contains("var12") ? vars["var12"].ToString() : "$.var12",
        ///     (vars) => ", ",
        ///     (vars) => vars.Contains("name") ? vars["name"].ToString() : "$.name",
        ///     (vars) => ".",
        /// ].
        /// </summary>
        private void TokenizeTemplate(IEnumerable<string> variableNames)
        {
            // We will start with the template as a whole and iteratively,
            // variable by variable, extract the variable names into TemplateTokens.
            // This list will contain template substrings left to process
            // interleaved with TemplateTokens.
            var substringsAndTokens = new List<object> { this.template };

            // Extract the longer names first. Otherwise, if one variable is a prefix of
            // another (e.g. Var1 and Var12), extracting "$.Var1" before "$.Var12" will
            // spoil all instances of "$.Var12".
            foreach (var varName in variableNames.OrderByDescending(s => s.Length))
            {
                string Substitute(Dictionary<string, object> variables)
                {
                    variables.TryGetValue(varName, out var result);

                    // If it is not able to get the value it returns $.{name of the variable}.
                    // Otherwise it converts the value to string using InvariantCulture.
                    // InvariantCulture is used to always serialize JSON-compatibly, e.g "1.8", not "1,8".
                    return result == null ? "$." + varName : Convert.ToString(result, CultureInfo.InvariantCulture);
                }

                // In all template substrings, replace all occurrences of $.varName
                // with the corresponding TemplateToken.
                substringsAndTokens = substringsAndTokens.SelectMany(elem =>
                {
                    if (elem is string substring)
                    {
                        var parts = substring.Split("$." + varName);
                        if (parts.Length > 1)
                        {
                            // Interleave all parts with the token
                            return parts
                                .SelectMany(p => new List<object> { p, (TemplateToken)Substitute })
                                .SkipLast(1); // SelectMany adds an extra token at the end - we don't need it
                        }
                    }

                    return new[] { elem };
                }).ToList();
            }

            // Micro-optimization - empty strings aren't useful.
            substringsAndTokens.RemoveAll(x => x is string substr && substr == string.Empty);

            this.templateTokens = substringsAndTokens.Select(
                elem => elem is TemplateToken token
                    ? token
                    : _ => elem.ToString()) // convert strings to dummy TemplateTokens for uniformity
                    .ToList();
        }
    }
}
