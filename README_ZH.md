# BedrockProtocol

[![License: MPL 2.0](https://img.shields.io/badge/License-MPL%202.0-brightgreen.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](Protocol.csproj)

> [English](README.md)

纯 C# 实现的 Minecraft 基岩版（Bedrock Edition）网络协议库，面向 .NET 10。为协议中的每个数据包提供强类型、开箱即用的编解码器，可直接用于开发服务端、客户端、代理与工具，无需手写序列化代码。

当前目标协议版本为 **2168**（见 `ProtocolVersion.VERSION`）。

本库可与我们的纯 C# 版 RakNet 实现搭配使用，后者负责基岩协议底层的可靠 UDP 传输。

---

## 特性

- **230+ 数据包编解码器** —— 每个已注册的数据包都有对应的强类型 C# 类，实现 `Read` / `Write`
- **AOT 友好的注册表** —— `PacketRegistry` 使用普通字典维护「包 ID ↔ 类型」映射，无反射
- **生成式代码** —— 编解码器由 [`Protocol.Gen`](https://github.com/ArkMirage) 依据协议定义自动生成
- **零依赖二进制 IO** —— 基于 Span 的 `MemoryStreamReader` / `MemoryStreamWriter` 与 VarInt 工具
- **忠实还原协议建模** —— 使用 `OneOf<...>` 可辨识联合与 `Optional<T>` 对应协议中的变体字段和可选字段

---

## 安装

### 环境要求

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) 或更高版本

### 添加到项目

```bash
dotnet add package ArkMirage.BedrockProtocol
```

或添加项目引用：

```xml
<ItemGroup>
  <ProjectReference Include="path/to/Protocol.csproj" />
</ItemGroup>
```

---

## 快速上手

### 解码收到的数据包

```csharp
using Protocol.Packets;

// packetId 来自你的传输层（例如 RakNet 帧负载）
IPacket packet = PacketRegistry.CreatePacket(packetId);
packet.Decode(rawBytes);

if (packet is TextPacket chat)
{
    // 以强类型方式访问所有字段
}
```

### 构造并编码要发送的数据包

```csharp
using Protocol.Packets;

var outgoing = new TextPacket
{
    // ...设置属性...
};

ReadOnlyMemory<byte> bytes = outgoing.Encode();
```

---

## 项目结构

| 路径 | 说明 |
|------|------|
| `Codec/IPacket.cs` | 数据包基类 —— 处理无符号 Varint 的 ID 前缀、`Encode` / `Decode` 及字节缓存 |
| `Codec/PacketRegistry.cs` | 静态注册表：「包 ID ↔ 类型」映射及工厂方法 |
| `Codec/MinecraftPacketIds.cs` | 全部数据包 ID 的枚举 |
| `Codec/Packet/` | 每个数据包一个编解码器（`LoginPacket`、`TextPacket`、`MovePlayerPacket` 等） |
| `Codec/Nbt/` | NBT（Named Binary Tag）编解码 |
| `Utility/` | 二进制读写（`MemoryStreamReader` / `MemoryStreamWriter`）、`VarInt`、`OneOf`、`Optional<T>` 等 |

---

## 许可证

本项目基于 [Mozilla Public License 2.0](LICENSE) 开源。
