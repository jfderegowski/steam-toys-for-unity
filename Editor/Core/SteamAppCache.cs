using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace SteamToys.Editor.Core
{
    /// <summary>
    /// Reads what the Steam client keeps in its <c>appcache</c> folder on this machine: data the
    /// client downloads for the apps it runs, and nothing this package writes itself.
    /// <para>
    /// The files are the client's own and undocumented, so everything here is read only and a file
    /// that cannot be read simply comes back as missing. Each kind of file lives in a partial of its
    /// own, e.g. <c>SteamAppCache.Stats.cs</c>.
    /// </para>
    /// </summary>
    public static partial class SteamAppCache
    {
        /// <summary>
        /// The folder Steam is installed in, e.g. <c>c:/program files (x86)/steam</c>, or null when
        /// Steam is not installed. Read from where the client registers itself, which only exists on
        /// Windows.
        /// </summary>
        public static string SteamPath =>
            _steamPath ??= Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;

        private static string _steamPath;

        /// <summary>
        /// The path of a file inside the <c>appcache</c> folder, or null when Steam is not installed.
        /// </summary>
        private static string GetPath(string folder, string fileName) =>
            SteamPath == null ? null : Path.Combine(SteamPath, "appcache", folder, fileName);

        #region Binary KeyValues

        // Valve's binary KeyValues: each entry is a type byte, a null terminated UTF-8 name and a value
        // whose layout the type decides. A section holds entries up to its end marker.
        private enum KeyValueType : byte
        {
            Section = 0,
            String = 1,
            Int32 = 2,
            Float32 = 3,
            Pointer = 4,
            Color = 6,
            UInt64 = 7,
            End = 8,
            Int64 = 10,
            AlternateEnd = 11
        }

        /// <summary>
        /// Parses a binary KeyValues file into nested dictionaries. Values come back as
        /// <c>string</c>, <c>int</c>, <c>float</c>, <c>long</c> or <c>ulong</c>, and names are
        /// compared ignoring case because the client writes the same key both ways, e.g.
        /// <c>Default</c> and <c>default</c>.
        /// </summary>
        private static Dictionary<string, object> ReadKeyValues(byte[] data)
        {
            var index = 0;

            return ReadSection(data, ref index);
        }

        private static Dictionary<string, object> ReadSection(byte[] data, ref int index)
        {
            var section = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // A file may stop without closing its outermost section.
            while (index < data.Length)
            {
                var type = (KeyValueType)data[index++];

                if (type is KeyValueType.End or KeyValueType.AlternateEnd)
                    break;

                var name = ReadString(data, ref index);

                object value;

                switch (type)
                {
                    case KeyValueType.Section:
                        value = ReadSection(data, ref index);
                        break;
                    case KeyValueType.String:
                        value = ReadString(data, ref index);
                        break;
                    case KeyValueType.Int32:
                    case KeyValueType.Pointer:
                    case KeyValueType.Color:
                        value = BitConverter.ToInt32(data, index);
                        index += 4;
                        break;
                    case KeyValueType.Float32:
                        value = BitConverter.ToSingle(data, index);
                        index += 4;
                        break;
                    case KeyValueType.UInt64:
                        value = BitConverter.ToUInt64(data, index);
                        index += 8;
                        break;
                    case KeyValueType.Int64:
                        value = BitConverter.ToInt64(data, index);
                        index += 8;
                        break;
                    default:
                        throw new InvalidDataException($"Unknown KeyValues type {(byte)type} at byte {index - 1}.");
                }

                section[name] = value;
            }

            return section;
        }

        private static string ReadString(byte[] data, ref int index)
        {
            var end = Array.IndexOf(data, (byte)0, index);

            if (end < 0)
                throw new InvalidDataException($"Unterminated KeyValues string at byte {index}.");

            var value = Encoding.UTF8.GetString(data, index, end - index);

            index = end + 1;

            return value;
        }

        #endregion
    }
}
