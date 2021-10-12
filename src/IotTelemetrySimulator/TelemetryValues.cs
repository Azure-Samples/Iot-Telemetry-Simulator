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

            ulong iterationNumber = 0;
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
                        next[val.Name] = this.random.Next((int)val.Min.Value, (int)val.Max.Value);
                    }
                    else
                    {
                        next[val.Name] = this.random.Next();
                    }
                }
                else if (val.RandomDouble)
                {
                    if (val.Min.HasValue && val.Max.HasValue && val.Max > val.Min)
                    {
                        next[val.Name] = this.random.NextDouble(val.Min.Value, val.Max.Value);
                    }
                    else
                    {
                        next[val.Name] = this.random.NextDouble();
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
                        var maxThres = val.Max ?? int.MaxValue;

                        switch (prevValue)
                        {
                            case int prevIntValue when prevIntValue > maxThres - step:
                                next[val.Name] = val.Min == null ? 1 : (int)val.Min;
                                break;

                            case int prevIntValue:
                                next[val.Name] = prevIntValue + step;
                                break;
                        }
                    }
                    else
                    {
                        next[val.Name] = val.Min == null ? 1 : (int)val.Min;
                    }
                }
            }

            // We generate values of sequence vars after the non-sequence vars, because
            // sequence vars might reference non-sequence vars.
            if (hasSequenceVars)
            {
                var notUsedSequenceVariables = this.Variables
                                                .Where(x => x.Sequence)
                                                .SelectMany(x => x.GetReferenceVariableNames())
                                                .ToHashSet();

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
                            notUsedSequenceVariables.Remove(usedVariable);
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
                }

                ResetNotUsedReferencedVariables(previous, next, notUsedSequenceVariables);
            }

            return next;
        }

        /// <summary>
        /// Removes non-used variables in a sequence.
        /// This way we can keep the a counter variable incrementally correctly if the sequence did not use it in current iteration.
        /// </summary>
        private static void ResetNotUsedReferencedVariables(
            Dictionary<string, object> previous,
            Dictionary<string, object> next,
            IEnumerable<string> notUsedVariables)
        {
            foreach (var notUsedVariable in notUsedVariables)
            {
                // Restore it from the previous value.
                if (previous != null && previous.TryGetValue(notUsedVariable, out var previousValue))
                {
                    next[notUsedVariable] = previousValue;
                }
                else
                {
                    next.Remove(notUsedVariable);
                }
            }
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
