using System;
using System.Collections.Generic;

namespace Protocol.Utility.IO
{
	/// <summary>列表读写辅助: 用方法调用替代生成的 for/foreach 循环代码。</summary>
	public static class SliceCodec
	{
		// ---- uvarint32 长度前缀 ----
		public static List<T> ReadSlice<T>(this MemoryStreamReader reader, Func<T> func)
		{
			uint count = VarInt.ReadUInt32(reader);
			var result = new List<T>((int)count);
			for (int i = 0; i < count; i++)
			{
				result.Add(func());
			}
			return result;
		}

		public static void WriteSlice<T>(this MemoryStreamWriter writer, IList<T> list, Action<T> action)
		{
			writer.WriteVarUInt32((uint)list.Count);
			foreach (T item in list)
			{
				action(item);
			}
		}

		// ---- uint32 长度前缀 ----
		public static List<T> ReadSliceUint32Length<T>(this MemoryStreamReader reader, Func<T> func)
		{
			uint count = reader.ReadUInt32();
			var result = new List<T>((int)count);
			for (int i = 0; i < count; i++)
			{
				result.Add(func());
			}
			return result;
		}

		public static void WriteSliceUint32Length<T>(this MemoryStreamWriter writer, IList<T> list, Action<T> action)
		{
			writer.WriteUInt32((uint)list.Count);
			foreach (T item in list)
			{
				action(item);
			}
		}

		// ---- 固定数量 ----
		public static T[] ReadSliceOfLen<T>(this MemoryStreamReader reader, int length, Func<T> func)
		{
			var result = new T[length];
			for (int i = 0; i < length; i++)
			{
				result[i] = func();
			}
			return result;
		}

		public static void WriteSliceOfLen<T>(this MemoryStreamWriter writer, T[] array, int length, Action<T> action)
		{
			for (int i = 0; i < length; i++)
			{
				action(array[i]);
			}
		}
	}
}
