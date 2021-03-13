namespace IotTelemetrySimulator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class TelemetryTemplate
    {
        private readonly string template;
        private List<object> templateTokens;

        public TelemetryTemplate(string template, IEnumerable<string> variableNames)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                throw new ArgumentException("Invalid _template", nameof(template));
            }

            this.template = template;
            this.TokenizeTemplate(variableNames);
        }

        public string Create(Dictionary<string, object> values)
        {
            return string.Join(
                string.Empty,
                this.templateTokens.Select(
                    token => token is Func<Dictionary<string, object>, string> varSubstitution
                    ? varSubstitution(values)
                    : token.ToString()));
        }

        internal string GetTemplateDefinition()
        {
            return this.template;
        }

        public override string ToString()
        {
            return this.template;
        }

        private void TokenizeTemplate(IEnumerable<string> variableNames)
        {
            this.templateTokens = new List<object> { this.template };

            // Replace the longer keys first. Otherwise, if one variable is a prefix of
            // another (e.g. Var1 and Var12), replacing "$.Var1" before "$.Var12" will
            // spoil all instances of "$.Var12".
            foreach (var key in variableNames.OrderByDescending(s => s.Length))
            {
                Func<Dictionary<string, object>, string> substituteFunc = translations => translations[key].ToString();

                this.templateTokens = this.templateTokens.SelectMany(token =>
                {
                    if (token is string stringToken)
                    {
                        var parts = stringToken.Split("$." + key);
                        if (parts.Length > 1)
                        {
                            return parts.SelectMany(p => new List<object> { p, substituteFunc }).SkipLast(1);
                        }
                    }

                    return new[] { token };
                }).ToList();
            }
        }
    }
}
