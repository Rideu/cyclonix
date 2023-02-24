using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cyclonix.Utils
{
    public static class JSON
    {
        public static string Stringify(object obj) => JsonConvert.SerializeObject(obj);

        public static T Parse<T>(string obj) => JsonConvert.DeserializeObject<T>(obj);

        public static JToken Parse(string obj) => JsonConvert.DeserializeObject<JToken>(obj);

        public static string ValueOf(this JToken jt, string key) => jt.Value<string>(key);

        public static KeyValuePair<string, JToken>[] ToKV(this JToken jt)
        {

            var ch = jt.Children<JObject>();
            var payload = new KeyValuePair<string, JToken>[ch.Count()];

            for (int i = 0; i < payload.Length; i++)
            {

                JObject o = ch.ElementAt(i);

                foreach (JProperty p in o.Properties())
                {

                    string name = p.Name;
                    JToken value = (JToken)p.Value;
                    payload[i] = new KeyValuePair<string, JToken>(name, value);
                }
            }

            return payload;
        }

        //public static string AsString(this JToken jt)
        //{ 
        //}
    }
}
