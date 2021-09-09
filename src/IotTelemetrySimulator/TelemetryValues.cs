namespace IotTelemetrySimulator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class TelemetryValues
    {
        private readonly IRandomizer random = new DefaultRandomizer();
        readonly string machineName;

        public IList<TelemetryVariable> Variables { get; }

        public TelemetryValues(IList<TelemetryVariable> variables)
        {
            this.Variables = variables;
            this.machineName = Environment.MachineName;
        }

        public Dictionary<string, object> NextValues(Dictionary<string, object> previous)
        {
            var next = new Dictionary<string, object>();
            var now = DateTime.Now;

            var iterationNumber = 0ul;
            if (previous != null && previous.TryGetValue(Constants.IterationNumberValueName, out var previousIterationNumber))
            {
                iterationNumber = (ulong)previousIterationNumber + 1;
            }

            next[Constants.TimeValueName] = now.ToUniversalTime().ToString("o");
            next[Constants.LocalTimeValueName] = now.ToString("o");
            next[Constants.TicksValueName] = now.Ticks;
            next[Constants.EpochValueName] = new DateTimeOffset(now).ToUnixTimeSeconds();
            next[Constants.GuidValueName] = Guid.NewGuid().ToString();
            next[Constants.MachineNameValueName] = this.machineName;
            next[Constants.IterationNumberValueName] = iterationNumber;

            if (previous != null)
            {
                next[Constants.DeviceIdValueName] = previous[Constants.DeviceIdValueName];
            }

            var hasSequenceVars = false;

            foreach (var val in this.Variables)
            {
                if (val.Sequence)
                {
                    hasSequenceVars = true;
                }
                else if (val.Random)
                {
                    if (val.Min.HasValue && val.Max.HasValue && val.Max > val.Min)
                    {
                        next[val.Name] = this.random.Next(val.Min.Value, val.Max.Value);
                    }
                    else
                    {
                        next[val.Name] = this.random.Next();
                    }
                }
                else if (val.CustomLengthString != null)
                {
                    next[val.Name] = this.CreateRandomString(val.CustomLengthString.Value);
                }
                else if (val.Values != null && val.Values.Length > 0)
                {
                    next[val.Name] = val.Values[this.random.Next(val.Values.Length)];
                }
                else
                {
                    if (previous != null && previous.TryGetValue(val.Name, out var prevValue))
                    {
                        var step = val.Step ?? 1;
                        switch (prevValue)
                        {
                            case int prevIntValue:
                                next[val.Name] = prevIntValue + step;
                                break;
                        }
                    }
                    else
                    {
                        next[val.Name] = val.Min ?? 1;
                    }
                }
            }

            if (hasSequenceVars)
            {
                foreach (var seqVar in this.Variables.Where(x => x.Sequence))
                {
                    var value = seqVar.Values[iterationNumber % (ulong)seqVar.Values.Length];
                    string usedVariable = null;
                    if (value is string valueString && valueString.StartsWith("$."))
                    {
                        usedVariable = valueString[2..];
                        if (next.TryGetValue(usedVariable, out var valueFromVariable))
                        {
                            next[seqVar.Name] = valueFromVariable;
                        }
                        else
                        {
                            next[seqVar.Name] = value;
                        }
                    }
                    else
                    {
                        next[seqVar.Name] = value;
                    }

                    var referencedVariables = seqVar.GetReferenceVariableNames();
                    foreach (var referenceVariable in referencedVariables)
                    {
                        if (referenceVariable != usedVariable)
                        {
                            if (previous != null && previous.TryGetValue(referenceVariable, out var previousValue))
                            {
                                next[referenceVariable] = previousValue;
                            }
                            else
                            {
                                next.Remove(referenceVariable);
                            }
                        }
                    }
                }
            }

            return next;
        }

        /// <summary>
        /// All possible variable names this object can produce.
        /// </summary>
        public IEnumerable<string> VariableNames()
        {
            return this.Variables.Select(v => v.Name).Concat(Constants.AllSpecialValueNames);
        }

        public string CreateRandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[this.random.Next(s.Length)]).ToArray());
        }
    }
}
