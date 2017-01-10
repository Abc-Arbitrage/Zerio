using System;
using System.Collections.Concurrent;
using System.Text;

namespace Abc.Zerio.Core
{
    public struct MessageTypeId : IEquatable<MessageTypeId>
    {
        private static readonly ConcurrentDictionary<Type, MessageTypeId> _values = new ConcurrentDictionary<Type, MessageTypeId>();
        private static Func<Type, MessageTypeId> _messageTypeIdFactory = CreateDefaultMessageTypeId;

        private readonly uint _value;

        public MessageTypeId(uint value)
        {
            _value = value;
        }

        public static MessageTypeId Get(Type type)
        {
            return _values.GetOrAdd(type, _messageTypeIdFactory);
        }

        public static void RegisterFactory(Func<Type, MessageTypeId> factory)
        {
            _messageTypeIdFactory = factory;
        }

        public static void Register(Type type, MessageTypeId messageTypeId)
        {
            _values[type] = messageTypeId;
        }

        public static explicit operator uint(MessageTypeId messageTypeId)
        {
            return messageTypeId._value;
        }

        public bool Equals(MessageTypeId other)
        {
            return _value == other._value;
        }

        public override bool Equals(object obj)
        {
            var other = obj as MessageTypeId?;
            return other != null && Equals(other.Value);
        }

        public override int GetHashCode()
        {
            return (int)_value;
        }

        private static unsafe MessageTypeId CreateDefaultMessageTypeId(Type messageType)
        {
            // Default implementation computes a crc32 hash from the type full name

            var messageTypeFullName = messageType.FullName;
            var fullNameLength = messageTypeFullName.Length;

            fixed (char* pChar = messageTypeFullName)
            {
                var bytes = stackalloc byte[fullNameLength];
                var length = Encoding.ASCII.GetBytes(pChar, fullNameLength, bytes, fullNameLength);

                return new MessageTypeId(Crc32.Compute(bytes, length));
            }
        }
    }
}
