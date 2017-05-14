using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace DJ.Utilities
{
	public class SettingsManager
	{
		
		/// <summary>
		/// The static settings manager
		/// </summary>
		public static class Settings
		{
			private static string _path = "./settings.json";
			private static readonly object Lock = new object();
			private static bool _init;
			private static Dictionary<string, object> _dict;

			/// <summary>
			/// Initialize the settingsmanager manually, with the option to specify the filepath of the settings file
			/// </summary>
			/// <param name="filename">The filename for the settings file</param>
			public static void Init(string filename = "./Settings.json")
			{
				if (_init) throw new Exception("Only call init once");
				_path = filename;
				Load();
				_init = true;
			}


			/// <summary>
			/// Get the element associated with the specified name
			/// </summary>
			/// <param name="name">The name of the element</param>
			/// <typeparam name="T">The type of the settings object stored (i.e. int, double, string, etc.)</typeparam>
			/// <returns></returns>
			public static T Get<T>(string name)
			{
				if (!_init)
					Init();
				object obj;
				if (!_dict.TryGetValue(name, out obj))
				{
					if (typeof(T) == typeof(bool))
					{
						obj = false;
					}
					else if (typeof(T) == typeof(int) || typeof(T) == typeof(uint))
					{
						obj = 0;
					}
					else
					{
						obj = default(T);
					}
				}
				return (T)obj;
			}

			/// <summary>
			/// Set the element associated with the specified name to specified object
			/// </summary>
			/// <param name="name">The name of the element</param>
			/// <param name="obj">The object to set it to</param>
			/// <typeparam name="T">Type of the object to store</typeparam>
			public static void Set<T>(string name, T obj)
			{
				if (!_init)
					Init();
				_dict[name] = obj;
				Save();
			}

			private static void Save()
			{
				var json = JsonConvert.SerializeObject(_dict);
				lock (Lock)
				{
					File.WriteAllText(_path, json);
				}
			}

			private static void Load()
			{
				if (File.Exists(_path))
					_dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(_path));
				if (_dict == null)
					_dict = new Dictionary<string, object>();
			}
		}
	}
}
