using System.Security.Cryptography;

namespace Protocol.Codec.Connection.Encryption
{
	public sealed class CryptoManager : IDisposable
	{
		private const int ChecksumSize = 8;
		private const int AesBlockSize = 16;

		private readonly object _mutex = new object();
		private readonly byte[] _initialCounterBlock = new byte[AesBlockSize];

		private bool _enabled;
		private ulong _encryptCounter;
		private ulong _decryptCounter;
		private byte[] _keyBytes = Array.Empty<byte>();
		private AesCtrState? _encryptState;
		private AesCtrState? _decryptState;

		public CryptoManager()
		{
		}

		public CryptoManager(byte[] keyBytes)
		{
			SetKeyBytes(keyBytes);
		}

		public bool IsEnabled
		{
			get
			{
				lock (_mutex)
				{
					return _enabled;
				}
			}
		}

		public void SetEnabled(bool enabled)
		{
			lock (_mutex)
			{
				_enabled = enabled;
			}
		}

		public void SetKeyBytes(byte[] keyBytes)
		{
			if (keyBytes == null)
			{
				throw new ArgumentNullException(nameof(keyBytes));
			}

			lock (_mutex)
			{
				_keyBytes = CopyOf(keyBytes);
				_encryptCounter = 0;
				_decryptCounter = 0;
				DisposeState(ref _encryptState);
				DisposeState(ref _decryptState);

				Array.Clear(_initialCounterBlock, 0, _initialCounterBlock.Length);
				int nonceSize = Math.Min(_keyBytes.Length, 12);
				if (nonceSize != 0)
				{
					Buffer.BlockCopy(_keyBytes, 0, _initialCounterBlock, 0, nonceSize);
				}

				_initialCounterBlock[15] = 0x02;
				_enabled = IsValidAesKeySize(_keyBytes.Length);
			}
		}

		public byte[] Encrypt(byte[] bytes)
		{
			if (bytes == null)
			{
				throw new ArgumentNullException(nameof(bytes));
			}

			lock (_mutex)
			{
				if (!_enabled || bytes.Length == 0)
				{
					return CopyOf(bytes);
				}

				byte[] data = new byte[bytes.Length + ChecksumSize];
				Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);

				byte[] sum = Checksum(_encryptCounter++, data, bytes.Length);
				Buffer.BlockCopy(sum, 0, data, bytes.Length, sum.Length);

				if (_encryptState == null && !InitializeCipher(ref _encryptState))
				{
					return CopyOf(bytes);
				}

				return CtrCrypt(_encryptState, data);
			}
		}

		public byte[] Decrypt(byte[] bytes)
		{
			if (bytes == null)
			{
				throw new ArgumentNullException(nameof(bytes));
			}

			lock (_mutex)
			{
				if (!_enabled || bytes.Length == 0)
				{
					return CopyOf(bytes);
				}

				if (bytes.Length < ChecksumSize)
				{
					return Array.Empty<byte>();
				}

				if (_decryptState == null && !InitializeCipher(ref _decryptState))
				{
					return CopyOf(bytes);
				}

				byte[] clear = CtrCrypt(_decryptState, bytes);
				if (!VerifyUnlocked(clear))
				{
					return Array.Empty<byte>();
				}

				Array.Resize(ref clear, clear.Length - ChecksumSize);
				return clear;
			}
		}

		public bool Verify(byte[] bytes)
		{
			if (bytes == null)
			{
				throw new ArgumentNullException(nameof(bytes));
			}

			lock (_mutex)
			{
				return VerifyUnlocked(bytes);
			}
		}

		public void Dispose()
		{
			lock (_mutex)
			{
				DisposeState(ref _encryptState);
				DisposeState(ref _decryptState);
			}
		}

		private byte[] Checksum(ulong counter, byte[] data, int dataSize)
		{
			byte[] result = new byte[ChecksumSize];

			try
			{
				byte[] input = new byte[sizeof(ulong) + dataSize + _keyBytes.Length];
				WriteLittleEndian64(input, counter);
				if (dataSize != 0)
				{
					Buffer.BlockCopy(data, 0, input, sizeof(ulong), dataSize);
				}

				if (_keyBytes.Length != 0)
				{
					Buffer.BlockCopy(_keyBytes, 0, input, sizeof(ulong) + dataSize, _keyBytes.Length);
				}

				using (SHA256 sha256 = SHA256.Create())
				{
					if (sha256 == null)
					{
						return result;
					}

					byte[] digest = sha256.ComputeHash(input);
					if (digest.Length >= ChecksumSize)
					{
						Buffer.BlockCopy(digest, 0, result, 0, ChecksumSize);
					}
				}
			}
			catch (CryptographicException)
			{
				return new byte[ChecksumSize];
			}

			return result;
		}

		private bool VerifyUnlocked(byte[] bytes)
		{
			if (!_enabled)
			{
				return true;
			}

			if (bytes.Length < ChecksumSize)
			{
				return false;
			}

			int payloadSize = bytes.Length - ChecksumSize;
			byte[] expected = Checksum(_decryptCounter++, bytes, payloadSize);

			for (int i = 0; i < ChecksumSize; ++i)
			{
				if (expected[i] != bytes[payloadSize + i])
				{
					return false;
				}
			}

			return true;
		}

		private bool InitializeCipher(ref AesCtrState? state)
		{
			if (!IsValidAesKeySize(_keyBytes.Length))
			{
				return false;
			}

			try
			{
				state = new AesCtrState(_keyBytes, _initialCounterBlock);
				return true;
			}
			catch (CryptographicException)
			{
				DisposeState(ref state);
				return false;
			}
		}

		private static byte[] CtrCrypt(AesCtrState? state, byte[] bytes)
		{
			if (bytes.Length == 0)
			{
				return Array.Empty<byte>();
			}

			if (state == null)
			{
				return CopyOf(bytes);
			}

			try
			{
				return state.Crypt(bytes);
			}
			catch (CryptographicException)
			{
				return CopyOf(bytes);
			}
		}

		private static bool IsValidAesKeySize(int keySize)
		{
			return keySize == 16 || keySize == 24 || keySize == 32;
		}

		private static byte[] CopyOf(byte[] bytes)
		{
			if (bytes.Length == 0)
			{
				return Array.Empty<byte>();
			}

			byte[] copy = new byte[bytes.Length];
			Buffer.BlockCopy(bytes, 0, copy, 0, bytes.Length);
			return copy;
		}

		private static void DisposeState(ref AesCtrState? state)
		{
			if (state != null)
			{
				state.Dispose();
				state = null;
			}
		}

		private static void WriteLittleEndian64(byte[] output, ulong value)
		{
			for (int i = 0; i < sizeof(ulong); ++i)
			{
				output[i] = (byte)((value >> (i * 8)) & 0xffUL);
			}
		}

		private sealed class AesCtrState : IDisposable
		{
			private readonly Aes _aes;
			private readonly ICryptoTransform _encryptor;
			private readonly byte[] _counterBlock = new byte[AesBlockSize];
			private readonly byte[] _keyStream = new byte[AesBlockSize];
			private int _keyStreamOffset = AesBlockSize;

			public AesCtrState(byte[] keyBytes, byte[] initialCounterBlock)
			{
				Buffer.BlockCopy(initialCounterBlock, 0, _counterBlock, 0, AesBlockSize);

				Aes aes = Aes.Create();
				if (aes == null)
				{
					throw new CryptographicException("AES provider is not available.");
				}

				try
				{
					aes.Mode = CipherMode.ECB;
					aes.Padding = PaddingMode.None;
					_encryptor = aes.CreateEncryptor(CopyOf(keyBytes), new byte[AesBlockSize]);
					_aes = aes;
				}
				catch
				{
					aes.Dispose();
					throw;
				}
			}

			public byte[] Crypt(byte[] input)
			{
				byte[] output = new byte[input.Length];

				for (int i = 0; i < input.Length; ++i)
				{
					if (_keyStreamOffset == AesBlockSize)
					{
						GenerateKeyStreamBlock();
					}

					output[i] = (byte)(input[i] ^ _keyStream[_keyStreamOffset]);
					++_keyStreamOffset;
				}

				return output;
			}

			public void Dispose()
			{
				_encryptor.Dispose();
				_aes.Dispose();
			}

			private void GenerateKeyStreamBlock()
			{
				int written = _encryptor.TransformBlock(_counterBlock, 0, AesBlockSize, _keyStream, 0);
				if (written != AesBlockSize)
				{
					throw new CryptographicException("AES-CTR keystream generation failed.");
				}

				IncrementCounterBlock(_counterBlock);
				_keyStreamOffset = 0;
			}

			private static void IncrementCounterBlock(byte[] counterBlock)
			{
				for (int i = counterBlock.Length - 1; i >= 0; --i)
				{
					counterBlock[i] = unchecked((byte)(counterBlock[i] + 1));
					if (counterBlock[i] != 0)
					{
						return;
					}
				}
			}
		}
	}
}