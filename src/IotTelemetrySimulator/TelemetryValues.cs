namespace IotTelemetrySimulator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class TelemetryValues
    {
        private IRandomizer random = new DefaultRandomizer();
        string machineName;

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

            next[Constants.TimeValueName] = now.ToUniversalTime().ToString("o");
            next[Constants.LocalTimeValueName] = now.ToString("o");
            next[Constants.TicksValueName] = now.Ticks;
            next[Constants.EpochValueName] = new DateTimeOffset(now).ToUnixTimeSeconds();
            next[Constants.GuidValueName] = Guid.NewGuid().ToString();
            next[Constants.MachineNameValueName] = this.machineName;

            if (previous != null)
            {
                next[Constants.DeviceIdValueName] = previous[Constants.DeviceIdValueName];
            }

            foreach (var val in this.Variables)
            {
                if (val.Random)
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

            return next;
        }

        public string CreateRandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[this.random.Next(s.Length)]).ToArray());
        }
    }
}
