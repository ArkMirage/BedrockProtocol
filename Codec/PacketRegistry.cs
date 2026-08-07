using System;
using System.Collections.Generic;
using System.Reflection;

namespace Protocol.Packets
{
	/// <summary>包注册表: 通过反射扫描程序集内所有 IPacket 实现, 按 PacketId 建立索引。</summary>
	public static class PacketRegistry
	{
		private static readonly Dictionary<int, Type> _packetsById = new Dictionary<int, Type>();
		private static readonly Dictionary<Type, int> _packetIdsByType = new Dictionary<Type, int>();

		static PacketRegistry()
		{
			foreach (var type in typeof(PacketRegistry).Assembly.GetTypes())
			{
				if (type.IsAbstract || type.IsInterface || !typeof(IPacket).IsAssignableFrom(type))
				{
					continue;
				}

				int packetId = ((IPacket)Activator.CreateInstance(type)).PacketId;
				_packetsById[packetId] = type;
				_packetIdsByType[type] = packetId;
			}
		}

		/// <summary>已注册的包数量。</summary>
		public static int Count => _packetsById.Count;

		/// <summary>PacketId → 包类型。</summary>
		public static IReadOnlyDictionary<int, Type> PacketsById => _packetsById;

		public static bool TryGetPacketType(int packetId, out Type type)
		{
			return _packetsById.TryGetValue(packetId, out type);
		}

		public static bool TryGetPacketType(MinecraftPacketIds packetId, out Type type)
		{
			return _packetsById.TryGetValue((int)packetId, out type);
		}

		public static Type GetPacketType(int packetId)
		{
			if (!_packetsById.TryGetValue(packetId, out var type))
			{
				throw new KeyNotFoundException($"未注册的包 ID: {packetId}");
			}

			return type;
		}

		public static Type GetPacketType(MinecraftPacketIds packetId)
		{
			return GetPacketType((int)packetId);
		}

		public static bool TryGetPacketId(Type type, out int packetId)
		{
			return _packetIdsByType.TryGetValue(type, out packetId);
		}

		public static int GetPacketId(Type type)
		{
			if (!_packetIdsByType.TryGetValue(type, out var packetId))
			{
				throw new KeyNotFoundException($"未注册的包类型: {type.FullName}");
			}

			return packetId;
		}

		public static int GetPacketId<T>() where T : IPacket
		{
			return GetPacketId(typeof(T));
		}

		public static IPacket CreatePacket(int packetId)
		{
			return (IPacket)Activator.CreateInstance(GetPacketType(packetId));
		}

		public static IPacket CreatePacket(MinecraftPacketIds packetId)
		{
			return CreatePacket((int)packetId);
		}

		public static T CreatePacket<T>() where T : IPacket, new()
		{
			return new T();
		}
	}
}
