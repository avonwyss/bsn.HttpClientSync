using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;

namespace bsn.HttpClientSync {
	/// <summary>
	/// Class for doing some shady reflection stuff which is unfortunately required to hook into the right places.
	/// </summary>
	internal static class ReflectionHelper<TType> {
		private static readonly Lazy<FieldInfo[]> Fields = new Lazy<FieldInfo[]>(() => typeof(StreamContent).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance), LazyThreadSafetyMode.PublicationOnly);

		public static TType CreateUninitialized() {
			return (TType)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(TType));
		}

		public static void CopyFields(TType source, TType destination) {
			// ReSharper disable CoVariantArrayConversion
			var data = System.Runtime.Serialization.FormatterServices.GetObjectData(source, Fields.Value);
			System.Runtime.Serialization.FormatterServices.PopulateObjectMembers(destination, Fields.Value, data);
			// ReSharper restore CoVariantArrayConversion
		}

		public static TDelegate GetPrivateMethod<TDelegate>(string name) where TDelegate: Delegate {
			var method = typeof(TType).GetMethod(name, BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance);
			if (method == null) {
				throw new MissingMethodException(typeof(TType).AssemblyQualifiedName, name);
			}
			return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), method);
		}
	}
}
