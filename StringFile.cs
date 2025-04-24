using System.Text;
using System.Text.Json.Serialization;
using System.Reflection;
using System.Text.Json;
using System.Reflection.PortableExecutable;
using System;
using System.Globalization;

namespace String2JSON
{
        public enum LocaleStringId : ulong { Invalid = 0, Blank = 0 }  // Currently unknown hash, used for localized strings
    public static class BinaryReaderExtensions
    {

        /// <summary>
        /// Read a null-terminated string at the current position.
        /// </summary>
        public static string ReadNullTerminatedString(this BinaryReader reader)
        {
            List<byte> byteList = new();

            while (true)
            {
                byte b = reader.ReadByte();
                if (b == 0x00) break;
                byteList.Add(b);
            }

            return Encoding.UTF8.GetString(byteList.ToArray());
        }

        /// <summary>
        /// Read a null-terminated string at the specified offset.
        /// </summary>
        public static string ReadNullTerminatedString(this BinaryReader reader, long offset)
        {
            long pos = reader.BaseStream.Position;              // Remember the current position
            reader.BaseStream.Seek(offset, 0);                  // Move to the offset
            string result = reader.ReadNullTerminatedString();  // Read the string
            reader.BaseStream.Seek(pos, 0);                     // Return to the original position
            return result;
        }
    }

        /// <summary>
        /// This JsonConverterFactory is required to allow the JSON deserializer to populate the 
        /// ID, StringMapEntry dictionary
        /// </summary>
        public class DictionaryTKeyEnumTValueConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            if (!typeToConvert.IsGenericType)
            {
                return false;
            }

            if (typeToConvert.GetGenericTypeDefinition() != typeof(Dictionary<,>))
            {
                return false;
            }

            return true;
        }

        public override JsonConverter CreateConverter(
            Type type,
            JsonSerializerOptions options)
        {
            Type[] typeArguments = type.GetGenericArguments();
            Type keyType = typeArguments[0];
            Type valueType = typeArguments[1];

            JsonConverter converter = (JsonConverter)Activator.CreateInstance(
                typeof(DictionaryEnumConverterInner<,>).MakeGenericType(
                    [keyType, valueType]),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: [options],
                culture: null)!;

            return converter;
        }

        private class DictionaryEnumConverterInner<TKey, TValue> :
            JsonConverter<Dictionary<TKey, TValue>> where TKey : struct, Enum
        {
            private readonly JsonConverter<TValue> _valueConverter;
            private readonly Type _keyType;
            private readonly Type _valueType;

            public DictionaryEnumConverterInner(JsonSerializerOptions options)
            {
                // For performance, use the existing converter.
                _valueConverter = (JsonConverter<TValue>)options
                    .GetConverter(typeof(TValue));

                // Cache the key and value types.
                _keyType = typeof(TKey);
                _valueType = typeof(TValue);
            }

            public override Dictionary<TKey, TValue> Read(
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException();
                }

                var dictionary = new Dictionary<TKey, TValue>();

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        return dictionary;
                    }

                    // Get the key.
                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        throw new JsonException();
                    }

                    string? propertyName = reader.GetString();

                    // For performance, parse with ignoreCase:false first.
                    if (!Enum.TryParse(propertyName, ignoreCase: false, out TKey key) &&
                        !Enum.TryParse(propertyName, ignoreCase: true, out key))
                    {
                        throw new JsonException(
                            $"Unable to convert \"{propertyName}\" to Enum \"{_keyType}\".");
                    }

                    // Get the value.
                    reader.Read();
                    TValue value = _valueConverter.Read(ref reader, _valueType, options)!;

                    // Add to dictionary.
                    dictionary.Add(key, value);
                }

                throw new JsonException();
            }

            public override void Write(
                Utf8JsonWriter writer,
                Dictionary<TKey, TValue> dictionary,
                JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                foreach ((TKey key, TValue value) in dictionary)
                {
                    string propertyName = key.ToString();
                    writer.WritePropertyName
                        (options.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName);

                    _valueConverter.Write(writer, value, options);
                }

                writer.WriteEndObject();
            }
        }
    }

    public class StringFile
    {
        public string Magic { get; set; }    // File signature
        public byte Version { get; set; }    // 10 for versions 1.9-1.17, 11 for 1.18+
        public ushort EntryCount {  get; set; }

        [JsonConverter(typeof(DictionaryTKeyEnumTValueConverter))]
        public Dictionary<LocaleStringId, StringMapEntry> StringMap { get; set; }

        public void GetStringFile(Stream stream)
        {
            using (BinaryReader reader = new(stream))
            {
                Magic = Encoding.UTF8.GetString(reader.ReadBytes(3));
                Version = reader.ReadByte();
                StringMap = new();

                EntryCount = reader.ReadUInt16();
                for (int i = 0; i < EntryCount; i++)
                {
                    var id = (LocaleStringId)reader.ReadUInt64();
                    StringMapEntry newMap=new();
                    newMap.GetStringMapEntry(reader);
                    StringMap.Add(id, newMap);
                }
            }
        }
/// <summary>
/// The beginning of a .string file has header consisting of 6 bytes 
/// Magic - 3 bytes containing STR
/// Version - 1 byte
/// EntryCount - 2 bytes containing the number of StringMapEntries
/// 
/// Each StringMapEntry has a file offset for each string it contains.
/// These strings are at the end of the file after all the StringMapEntry
/// 
/// This function calculates where all the strings WILL BE when the file is written
/// and stores the appropriate offset for each string
/// </summary>
        public void CalculateStringOffsets()
        {
            int headerlen = 6;
            int textPos = 0;

            //add up the size of all the StringMapEntry
            //have to loop through them because number of Variants is variable
            for (int i = 0; i < EntryCount; i++)
            {
                var pair = StringMap.ElementAt(i);
                StringMapEntry thisMap = pair.Value;
                //ID
                headerlen += 8;
                //Variant Count
                headerlen += 2;
                //FProduce
                headerlen += 2;
                //String Offset
                headerlen += 4;
                for (int j = 0; j < thisMap.Variants.Length; j++)
                {
                    headerlen += 8 + 2 + 4;
                }
            }

            //All the header information is finished
            //Strings would start here so initial textPos is the headerlen just calculated
            textPos = headerlen;

            //For each StringMapEntry, set its StringOffset to the current textPos
            //and then calculate the length of the string and set the textPos for the next StringMapEntry
            for (int i = 0; i < EntryCount; i++)
            {
                var pair = StringMap.ElementAt(i);
                StringMapEntry thisMap = pair.Value;
                thisMap.SetStringOffset(textPos);
//                stringInfo = new(thisMap.String);

                //Each character in the string can be written as 1 or more bytes
                //A simple string.Length does not reflect the size that may be written
                //Use BinaryWriter to write the string to a memory stream and then get the
                //length of that stream
                //add 1 because a null is written after the string
                using (MemoryStream stream = new MemoryStream())
                {
                    using (BinaryWriter writer = new BinaryWriter(stream))
                    {
                        for (int j = 0; j < thisMap.String.Length; j++)
                        {
                            writer.Write((char)thisMap.String[j]);
                        }
                        textPos += (int)writer.BaseStream.Length;
                    }
                }
                textPos++;

                //Check for any Variants in the StringMapEntry
                //A variant also has a string and a StringOffset
                //need to calculate the offset and length
                //the same as for StringMapEntry
                for (int j = 0; j < thisMap.Variants.Length; j++)
                {
                    thisMap.Variants[j].SetStringOffset(textPos);
                    using (MemoryStream stream = new MemoryStream())
                    {
                        using (BinaryWriter writer = new BinaryWriter(stream))
                        {
                            for (int k = 0; k < thisMap.Variants[j].String.Length; k++)
                            {
                                writer.Write((char)thisMap.Variants[j].String[k]);
                            }
                            textPos += (int)writer.BaseStream.Length;
                        }
                     }
                    textPos++;
                }
            }
            //All the StringMapEntry offsets are set, this can be written for real now
        }

/// <summary>
/// Write this StringFile back out as a .string file
/// </summary>
/// <param name="stream"></param>
        public void WriteStringFile(Stream stream)
        {
            CalculateStringOffsets();

            using (BinaryWriter writer = new(stream))
            {
                //Write the fixed header
                writer.Write(Magic[0]);
                writer.Write(Magic[1]);
                writer.Write(Magic[2]);
                writer.Write(Version);
                writer.Write(EntryCount);

                //Write out all the StringMapEntry
                for (int i = 0; i < EntryCount; i++)
                {
                    var pair = StringMap.ElementAt(i);
                    UInt64 localeStringId = (UInt64)(ulong)pair.Key;
                    writer.Write(localeStringId);
                    StringMapEntry thisMap = pair.Value;
                    ushort varlen = (ushort)(thisMap.Variants.Length);
                    varlen++;
                    writer.Write(varlen);

                    writer.Write(thisMap.FlagsProduced);
                    writer.Write(thisMap.GetStringOffset());
                    for (int j = 0; j < thisMap.Variants.Length; j++)
                    {
                        writer.Write(thisMap.Variants[j].FlagsConsumed);
                        writer.Write(thisMap.Variants[j].FlagsProduced);
                        writer.Write(thisMap.Variants[j].GetStringOffset());
                    }
                }

                //Write all the strings, null terminated
                for (int i = 0; i < EntryCount; i++)
                {
                    var pair = StringMap.ElementAt(i);
                    StringMapEntry thisMap = pair.Value;

                    for(int j=0;j<thisMap.String.Length; j++)
                    {
                        writer.Write((char)thisMap.String[j]);
                    }
                    writer.Write((char)0);

                    for (int j = 0; j < thisMap.Variants.Length; j++)
                    {
                        for (int k = 0; k < thisMap.Variants[j].String.Length; k++)
                        {
                            writer.Write((char)thisMap.Variants[j].String[k]);
                        }
                        writer.Write((char)0);
                    }
                }
            }
        }
        public StringFile(string magic, byte version,ushort entryCount, Dictionary<LocaleStringId, StringMapEntry> stringMap) 
        {
            Magic = magic;
            Version = version;
            EntryCount = entryCount;
            StringMap = new(stringMap);
        }
        public StringFile() 
        {
            Magic = "";
            StringMap = new();
        }
    }
/// <summary>
/// This class holds a String ID and its associated string
/// </summary>
    public class StringMapEntry
    {
        public StringVariation[] Variants { get; set; }
        public ushort FlagsProduced { get; set; }
        public string String { get; set; }

        // StringOffset is private so that the JSON serializer/deserialzer doesn't see it since it is a derived value
        private UInt32 StringOffset { get; set; }

        /// <summary>
        /// Need a parameterized initializer for the JSON deserializer
        /// </summary>
        /// <param name="variants"></param>
        /// <param name="flagsProduced"></param>
        /// <param name="strinG"></param>
        public StringMapEntry() 
        { 
            Variants= Array.Empty<StringVariation>();
            String = "";
        }
        public StringMapEntry(StringVariation[] variants,ushort flagsProduced,string strinG)
        {
            var variantNum = variants.Length;
            Variants = variantNum > 0
                ? new StringVariation[variantNum - 1]
                : Array.Empty<StringVariation>();
            Variants = variants;
            FlagsProduced = flagsProduced;
            String = strinG;
        }

        public void SetStringOffset(int offset)
        {
            StringOffset = (UInt32)offset;
        }
        public UInt32 GetStringOffset()
        {
            return StringOffset;
        }
        public void GetStringMapEntry(BinaryReader reader)
        {
            ushort variantNum = reader.ReadUInt16();
            Variants = variantNum > 0
                ? new StringVariation[variantNum - 1]
                : Array.Empty<StringVariation>();

            FlagsProduced = reader.ReadUInt16();
            var stringLoc = reader.ReadUInt32();
            if (stringLoc <= reader.BaseStream.Length)
                String = reader.ReadNullTerminatedString(stringLoc);
            else
                Console.WriteLine("Possible .string file corruption.  String beyond end of file");

            for (int i = 0; i < Variants.Length; i++)
            {
                Variants[i] = new();
                Variants[i].GetStringVariation(reader);
            }
        }
    }
    /// <summary>
    /// Class holds any variants, a StringMapEntry may have multiple variants
    /// </summary>
    public class StringVariation
    {
        public ulong FlagsConsumed { get; set; }
        public ushort FlagsProduced { get; set; }
        public string String { get; set; }

        // StringOffset is private so that the JSON serializer/deserialzer doesn't see it since it is a derived value
        private UInt32 StringOffset { get; set; }

        /// <summary>
        /// Class needs a parameterized initializer for the JSON deserializer
        /// </summary>
        public StringVariation() 
        {
            String = "";
        }
        public StringVariation(ulong flagsConsumed, ushort flagsProduced, string StrinG)
        {
            FlagsConsumed = flagsConsumed;
            FlagsProduced = flagsProduced;
            String = StrinG;
        }
        public void SetStringOffset(int offset)
        {
            StringOffset = (UInt32)offset;
        }
        public UInt32 GetStringOffset()
        {
            return StringOffset;
        }

        public void GetStringVariation(BinaryReader reader)
        {
            FlagsConsumed = reader.ReadUInt64();
            FlagsProduced = reader.ReadUInt16();
            var stringLoc = reader.ReadUInt32();
            if (stringLoc<= reader.BaseStream.Length)
                String = reader.ReadNullTerminatedString(stringLoc);
            else
                Console.WriteLine("Possible .string file corruption.  Variant string beyond end of file");
        }
    }
}
