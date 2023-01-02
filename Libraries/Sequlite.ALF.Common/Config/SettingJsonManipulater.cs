using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
   
    public class SettingJsonManipulater
    {
        public SettingJsonManipulater() { }

        public async Task<T> ReadSettingsFromFile<T>(string fileName)
        {
            using (FileStream fs = File.OpenRead(fileName))
            {
                var stringEnumConverter = new System.Text.Json.Serialization.JsonStringEnumConverter();

                JsonSerializerOptions opts = new JsonSerializerOptions()
                {
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                };
                opts.Converters.Add(stringEnumConverter);
                opts.Converters.Add(new JsonNonStringKeyDictionaryConverterFactory());
                return await JsonSerializer.DeserializeAsync<T>(fs, opts);
            }
        }

        //public SystemConfigJson ReadConfigFromFile2(string fileName)
        //{
        //    string jsonString = File.ReadAllText(fileName);
        //    var stringEnumConverter = new System.Text.Json.Serialization.JsonStringEnumConverter();

        //    JsonSerializerOptions opts = new JsonSerializerOptions()
        //    {
        //        ReadCommentHandling = JsonCommentHandling.Skip,
        //        AllowTrailingCommas = true,
        //    };
        //    opts.Converters.Add(stringEnumConverter);
        //    opts.Converters.Add(new JsonNonStringKeyDictionaryConverterFactory());
        //    return JsonSerializer.Deserialize<SystemConfigJson>(jsonString, opts);
           
        //}

        public async Task SaveSettingsToFile<T>(T settings, string fileName)
        {
            using (FileStream fs = File.Create(fileName))
            {
                var stringEnumConverter = new System.Text.Json.Serialization.JsonStringEnumConverter();
                JsonSerializerOptions opts = new JsonSerializerOptions()
                {
                    WriteIndented = true,
                };
                opts.Converters.Add(stringEnumConverter);
                opts.Converters.Add(new JsonNonStringKeyDictionaryConverterFactory());
                //opts.Converters.Add(new DecimalFormatConverter());
                //opts.Converters.Add(new DoubleFormatConverter());
                //opts.Converters.Add(new FloatFormatConverter());
                await JsonSerializer.SerializeAsync(fs, settings, opts);
            }

        }
    }
}
