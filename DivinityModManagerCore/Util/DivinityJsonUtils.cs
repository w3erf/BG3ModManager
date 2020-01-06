﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DivinityModManager.Util
{
	public static class DivinityJsonUtils
	{
		public static T GetValue<T>(this JToken jToken, string key, T defaultValue = default(T))
		{
			dynamic ret = jToken[key];
			if (ret == null) return defaultValue;
			if (ret is JObject) return JsonConvert.DeserializeObject<T>(ret.ToString());
			return (T)ret;
		}

		public static T SafeDeserialize<T>(string text)
		{
			List<string> errors = new List<string>();

			var result = JsonConvert.DeserializeObject<T>(text, new JsonSerializerSettings
			{
				Error = delegate(object sender, ErrorEventArgs args)
				{
					errors.Add(args.ErrorContext.Error.Message);
					args.ErrorContext.Handled = true;
				}
			});
			if(result != null)
			{
				return result;
			}
			else
			{
				Trace.WriteLine("Error deserializing json:\n\t" + String.Join("\n\t", errors));
				return default(T);
			}
		}
	}
}