namespace IotTelemetrySimulator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // A function that represents a part of the template.
    // It accepts variable values and returns this part of the template with the variables substituted.
    using TemplateToken = System.Func<System.Collections.Generic.Dictionary<string, object>, string>;

    public class TelemetryTemplate
    {
        private readonly string template;

        // Internal representation of the template that allows fast variable substitution.
        private List<TemplateToken> templateTokens;

        public TelemetryTemplate(string template, IEnumerable<string> variableNames)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                throw new ArgumentException("Invalid _template", nameof(template));
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
                this.templateTokens.Select(varSubstitution => varSubstitution(variables)));
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
        ///     (vars) => vars["name"],
        ///     (vars) => "! I like ",
        ///     (vars) => "vars["var1"]",
        ///     (vars) => ", ",
        ///     (vars) => "vars["var11"]",
        ///     (vars) => " and ",
        ///     (vars) => "vars["var12"]",
        ///     (vars) => ", ",
        ///     (vars) => "vars["name"]",
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
                TemplateToken varSubstitution = variables => variables[varName].ToString();

                // In all template substrings, replace all occurrences of $.varName
                // with the corresponding TemplateToken.
                substringsAndTokens = substringsAndTokens.SelectMany(token =>
                {
                    if (token is string stringToken)
                    {
                        var parts = stringToken.Split("$." + varName);
                        if (parts.Length > 1)
                        {
                            // Interleave all parts with varSubstitution
                            return parts
                                .SelectMany(p => new List<object> { p, varSubstitution })
                                .SkipLast(1); // SelectMany adds an extra varSubstitution at the end - we don't need it
                        }
                    }

                    return new[] { token };
                }).ToList();
            }

            // Micro-optimization - empty strings aren't useful.
            substringsAndTokens.RemoveAll(x => x is string substr && substr == string.Empty);

            this.templateTokens = substringsAndTokens.Select(
                substringOrToken => substringOrToken is TemplateToken token
                    ? token
                    : _ => substringOrToken.ToString()) // convert strings to dummy TemplateTokens for uniformity
                    .ToList();
        }
    }
}
