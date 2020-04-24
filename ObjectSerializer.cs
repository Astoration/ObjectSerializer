using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

public static class ObjectSerializer
{
	public static int CurrentIndex { get; set; } = 0;

	public static bool IsDictionary(this Type type)
	{
		return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>);
	}
	public static bool IsList(this Type type)
	{
		return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
	}
	public static Type GetDictionarKeyType(this Type type)
	{
		if (!type.IsDictionary()) return null;
		return type.GetGenericArguments()[0];
	}
	public static Type GetDictionarValueType(this Type type)
	{
		if (!type.IsDictionary()) return null;
		return type.GetGenericArguments()[1];
	}
	public static Type GetListType(this Type type)
	{
		if (!type.IsList()) return null;
		return type.GetGenericArguments()[0];
	}

	public static object GetObjectFromByte<T>(this byte[] _buffer) where T : new()
	{
		CurrentIndex = 0;
		return GetObject(_buffer, typeof(T));
	}

	public static T GetObject<T>(this byte[] _buffer) where T : new()
	{
		CurrentIndex = 0;
		return (T)GetObject(_buffer, typeof(T));
	}

	public static byte[] Combine(this byte[] origin, byte[] target)
	{
		byte[] rv = new byte[origin.Length + target.Length];
		int offset = 0;
		foreach (byte[] array in new byte[][] { origin, target })
		{
			System.Buffer.BlockCopy(array, 0, rv, offset, array.Length);
			offset += array.Length;
		}
		return rv;
	}

	public static byte[] ToByteArray<T>(this object _object)
	{
		return _object.ToByteArray(typeof(T));
	}

	public static byte[] ToByteArray(this object _object, Type type)
	{
		byte[] result = new byte[0];
		switch (Type.GetTypeCode(type))
		{
			case TypeCode.Boolean:
				if (_object == null) return new byte[sizeof(bool)];
				return BitConverter.GetBytes(((bool)_object));
			case TypeCode.Int32:
				if (_object == null) return new byte[sizeof(int)];
				result = BitConverter.GetBytes(((int)_object));
				Array.Reverse(result);
				return result;
			case TypeCode.Int64:
				if (_object == null) return new byte[sizeof(long)];
				result = BitConverter.GetBytes(((long)_object));
				Array.Reverse(result);
				return result;
			case TypeCode.Double:
				if (_object == null) return new byte[sizeof(double)];
				result = BitConverter.GetBytes(((double)_object));
				Array.Reverse(result);
				return result;
			case TypeCode.String:
				var content = Encoding.UTF8.GetBytes(((string)_object));
				var contentSize = content.Length;
				result = contentSize.ToByteArray<int>();
				result = result.Combine(content);
				return result;
			case TypeCode.Object:
				if (type.IsList())
				{
					Type listType = type.GetListType();
					var propertyCount = type.GetProperty("Count");
					var count = propertyCount.GetValue(_object);
					result = count.ToByteArray<int>();
					ICollection list = _object as ICollection;
					foreach (var item in list)
                    {
						result = result.Combine(item.ToByteArray(listType));
                    }
				}
				else
				{
					var fields = type.GetFields();
					foreach (var field in fields)
					{
						var fieldValue = field.GetValue(_object);
						result = result.Combine(fieldValue.ToByteArray(field.FieldType));
					}
				}
				return result;
			default:
				return null;
		}
	}

	public static object GetObject(this byte[] _buffer, Type type)
	{
		switch (Type.GetTypeCode(type))
		{
			case TypeCode.Boolean:
				return _buffer.GetBoolean();
			case TypeCode.Int32:
				return _buffer.GetInt32();
			case TypeCode.Int64:
				return _buffer.GetInt64();
			case TypeCode.Double:
				return _buffer.GetDouble();
			case TypeCode.String:
				return _buffer.GetString();
			case TypeCode.Object:
				object instance;
				if (type.IsDictionary())
				{
					Type keyType = type.GetDictionarKeyType();
					Type valueType = type.GetDictionarValueType();
					instance = _buffer.GetDictionary(keyType, valueType);
				}
				else if (type.IsList())
				{
					Type listType = type.GetListType();
					instance = _buffer.GetList(listType);
				}
				else
				{
					instance = Activator.CreateInstance(type);
					var fields = type.GetFields();
					foreach (var field in fields)
					{
						var fieldValue = _buffer.GetObject(field.FieldType);
						field.SetValue(instance, fieldValue);
					}
				}
				return instance;
			default:
				return null;
		}
	}

	public static int GetInt32(this byte[] _buffer)
	{
		byte[] buffer = new byte[4];
		Array.Copy(_buffer, CurrentIndex, buffer, 0, sizeof(int));
		Array.Reverse(buffer);
		CurrentIndex += sizeof(int);
		return BitConverter.ToInt32(buffer, 0);
	}

	public static long GetInt64(this byte[] _buffer)
	{
		byte[] buffer = new byte[8];
		Array.Copy(_buffer, CurrentIndex, buffer, 0, sizeof(long));
		Array.Reverse(buffer);
		CurrentIndex += sizeof(long);
		return BitConverter.ToInt64(buffer, 0);
	}

	public static double GetDouble(this byte[] _buffer)
	{
		byte[] buffer = new byte[8];
		Array.Copy(_buffer, CurrentIndex, buffer, 0, sizeof(double));
		CurrentIndex += sizeof(double);
		Array.Reverse(buffer);
		return BitConverter.ToDouble(buffer, 0);
	}

	public static bool GetBoolean(this byte[] _buffer)
	{
		byte[] buffer = new byte[1];
		Array.Copy(_buffer, CurrentIndex, buffer, 0, sizeof(bool));
		CurrentIndex += sizeof(bool);
		return BitConverter.ToBoolean(buffer, 0);
	}

	public static string GetString(this byte[] _buffer)
	{
		int length = _buffer.GetInt32();
		byte[] buffer = new byte[length];
		Array.Copy(_buffer, CurrentIndex, buffer, 0, length);
		CurrentIndex += length;
		return Encoding.UTF8.GetString(buffer);
	}

	public static object GetList(this byte[] _buffer, Type type)
	{
		int count = _buffer.GetInt32();
		var listType = typeof(List<>).MakeGenericType(type);
		var list = Activator.CreateInstance(listType);
		for (var i = 0; i < count; i++)
		{
			var obj = _buffer.GetObject(type);
			listType.GetMethod("Add").Invoke(list, new object[] { obj });
		}
		return list;
	}
	public static List<T> GetList<T>(this byte[] _buffer) where T : class, new()
	{
		int count = _buffer.GetInt32();
		List<T> list = new List<T>();

		for (var i = 0; i < count; i++)
		{
			T t = _buffer.GetObject<T>() as T;

			list.Add(t);
		}

		return list;
	}

	public static Dictionary<K, V> GetDictionary<K, V>(this byte[] _buffer, V _value) where K : struct
																					  where V : class, new()
	{
		int count = _buffer.GetInt32();
		Dictionary<K, V> dictionary = new Dictionary<K, V>();
		for (var i = 0; i < count; i++)
		{
			K key = (K)_buffer.GetObject<K>();
			V value = _buffer.GetObject<V>() as V;
			dictionary.Add(key, value);
		}
		return dictionary;
	}

	public static object GetDictionary(this byte[] _buffer, Type keyType, Type valueType)
	{
		int count = _buffer.GetInt32();
		var dictionaryType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
		var dictionary = Activator.CreateInstance(dictionaryType);
		for (var i = 0; i < count; i++)
		{
			object key = _buffer.GetObject(keyType);
			object value = _buffer.GetObject(valueType);
			if ((key == null) || (value == null)) return null;
			var hasKey = (bool)dictionaryType.GetMethod("ContainsKey").Invoke(dictionary, new object[] { key });
			if (hasKey) continue;
			dictionaryType.GetMethod("Add").Invoke(dictionary, new object[] { key, value });
		}
		return dictionary;
	}

	public static Dictionary<string, V> GetDictionary<V>(this byte[] _buffer) where V : struct
	{
		int count = _buffer.GetInt32();
		Dictionary<string, V> dictionary = new Dictionary<string, V>();

		for (var i = 0; i < count; i++)
		{
			string key = _buffer.GetString();
			V value = (V)_buffer.GetObject<V>();

			dictionary.Add(key, value);
		}
		return dictionary;
	}

	public static Dictionary<K, V> GetDictionary<K, V>(this byte[] _buffer) where K : struct
																			where V : struct
	{
		int count = _buffer.GetInt32();
		Dictionary<K, V> dictionary = new Dictionary<K, V>();
		for (var i = 0; i < count; i++)
		{
			K key = (K)_buffer.GetObject<K>();
			V value = (V)_buffer.GetObject<V>();
			dictionary.Add(key, value);
		}
		return dictionary;
	}
}
