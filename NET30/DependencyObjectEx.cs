﻿/******************************************************************************
*	Copyright 2013 Prospective Software Inc.
*	Licensed under the Apache License, Version 2.0 (the "License");
*	you may not use this file except in compliance with the License.
*	You may obtain a copy of the License at
*
*		http://www.apache.org/licenses/LICENSE-2.0
*
*	Unless required by applicable law or agreed to in writing, software
*	distributed under the License is distributed on an "AS IS" BASIS,
*	WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
*	See the License for the specific language governing permissions and
*	limitations under the License.
******************************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace System.Windows
{
	public class DependencyObjectEx : DependencyObject
	{
		private readonly Dictionary<int, object> values;
		private readonly ReaderWriterLock exvallock;

		public DependencyObjectEx()
		{
			values = new Dictionary<int, object>();
			exvallock = new ReaderWriterLock();
		}

		public object GetValueThreaded(DependencyProperty dp)
		{
			if (Application.Current.Dispatcher.CheckAccess()) return GetValue(dp);
			object ret = null;
			Application.Current.Dispatcher.Invoke(new Action<object>((t) => { ret = GetValue(dp); }), DispatcherPriority.Normal);
			return ret;
		}
		
		public void SetValueThreaded(DependencyProperty dp, object value)
		{
			if(Application.Current.Dispatcher.CheckAccess()) SetValue(dp, value);
			Application.Current.Dispatcher.Invoke(new Action<object>((t) => SetValue(dp, value)), DispatcherPriority.Normal);
		}

		public void SetValueThreaded(DependencyPropertyKey dp, object value)
		{
			if (Application.Current.Dispatcher.CheckAccess()) SetValue(dp, value);
			Application.Current.Dispatcher.Invoke(new Action<object>((t) => SetValue(dp, value)), DispatcherPriority.Normal);
		}

		public object GetValueExternal<T>(DependencyExternal<T> de)
		{
			if (de.IsUnset)
				throw new ArgumentException(string.Format("External value '{0}' in type '{1}' is unset. You must set a value before accessing the property.", de.Name, de.OwnerType));

			exvallock.AcquireWriterLock(0);
			try
			{
				object value;
				return values.TryGetValue(de.GetHashCode(), out value) == false ? de.DefaultValue : value;
			}
			finally
			{
				exvallock.ReleaseReaderLock();
			}
		}
		
		public void SetValueExternal<T>(DependencyExternal<T> de, T value)
		{
			if (de.ExternalValidateValueCallback != null)
			{
				var t = new ExternalValidateValueArgs<T>(this, value);
				de.ExternalValidateValueCallback(t);
				if (!t.IsValid) return;
			}

			exvallock.AcquireWriterLock(0);
			try
			{
				if (EqualityComparer<T>.Default.Equals(value, de.DefaultValue))
				{
					object temp;
					if (values.TryGetValue(de.GetHashCode(), out temp) && de.ExternalPropertyChangedCallback != null)
					{
						values.Remove(de.GetHashCode());
						de.ExternalPropertyChangedCallback(new ExternalPropertyChangedArgs<T>(this, (T)temp, de.DefaultValue));
					}
				}
				else
				{
					if (values.ContainsKey(de.GetHashCode()))
					{
						object temp;
						values.TryGetValue(de.GetHashCode(), out temp);
						values.Remove(de.GetHashCode());
						values.Add(de.GetHashCode(), value);
						if (de.ExternalPropertyChangedCallback != null) de.ExternalPropertyChangedCallback(new ExternalPropertyChangedArgs<T>(this, (T)temp, value));
					}
					else
					{
						values.Add(de.GetHashCode(), value);
						if (de.ExternalPropertyChangedCallback != null) de.ExternalPropertyChangedCallback(new ExternalPropertyChangedArgs<T>(this, de.DefaultValue, value));
					}
				}
			}
			finally
			{
				exvallock.ReleaseReaderLock();
			}

			de.IsUnset = false;
		}

		public void ClearExternalValue<T>(DependencyExternal<T> de)
		{
			de.IsUnset = true;

			exvallock.AcquireWriterLock(0);
			try
			{
				object temp;
				if (values.TryGetValue(de.GetHashCode(), out temp) && values.Remove(de.GetHashCode()) && de.ExternalPropertyChangedCallback != null) de.ExternalPropertyChangedCallback(new ExternalPropertyChangedArgs<T>(this, de.DefaultValue, (T)temp));
			}
			finally
			{
				exvallock.ReleaseReaderLock();
			}
		}
	}

	public class DependencyExternalBase
	{
		protected static readonly Dictionary<int, object> registered;
		protected static readonly ReaderWriterLock reglock;

		static DependencyExternalBase()
		{
			registered = new Dictionary<int, object>();
			reglock = new ReaderWriterLock();
		}
	}

	public sealed class DependencyExternal<T> : DependencyExternalBase
	{
		public string Name { get; private set; }
		public Type OwnerType { get; private set; }
		public Type PropertyType { get; private set; }
		public T DefaultValue { get; private set; }
		private int isUnset = 0;
		public bool IsUnset { get { if (isUnset == 0) { return false; } return true; } internal set { if (value) { Interlocked.CompareExchange(ref isUnset, 1, 0); } else {Interlocked.CompareExchange(ref isUnset, 0, 1);} } }
	
		internal Action<ExternalPropertyChangedArgs<T>> ExternalPropertyChangedCallback;
		internal Action<ExternalValidateValueArgs<T>> ExternalValidateValueCallback;

		public DependencyExternal() { }

		private DependencyExternal(string Name, Type OwnerType)
		{
			this.Name = Name;
			this.OwnerType = OwnerType;
			PropertyType = typeof(T);
			DefaultValue = default(T);
			ExternalPropertyChangedCallback = null;
			ExternalValidateValueCallback = null;
		}

		private DependencyExternal(string Name, Type OwnerType, T DefaultValue)
		{
			this.Name = Name;
			this.OwnerType = OwnerType;
			PropertyType = typeof(T);
			this.DefaultValue = DefaultValue;
			ExternalPropertyChangedCallback = null;
			ExternalValidateValueCallback = null;
		}

		private DependencyExternal(string Name, Type OwnerType, T DefaultValue, Action<ExternalPropertyChangedArgs<T>> ExternalPropertyChangedCallback)
		{
			this.Name = Name;
			this.OwnerType = OwnerType;
			PropertyType = typeof(T);
			this.DefaultValue = DefaultValue;
			ExternalPropertyChangedCallback = null;
			ExternalValidateValueCallback = null;
		}

		private DependencyExternal(string Name, Type OwnerType, T DefaultValue, Action<ExternalPropertyChangedArgs<T>> ExternalPropertyChangedCallback, Action<ExternalValidateValueArgs<T>> ExternalValidateValueCallback)
		{
			this.Name = Name;
			this.OwnerType = OwnerType;
			PropertyType = typeof(T);
			this.DefaultValue = DefaultValue;
			ExternalPropertyChangedCallback = null;
			ExternalValidateValueCallback = null;
		}

		public static DependencyExternal<TType> RegisterExternal<TType>(string name, Type ownerType)
		{
			var np = new DependencyExternal<TType>(name, ownerType);
			reglock.AcquireWriterLock(0);
			try
			{
				if (registered.ContainsKey(np.GetHashCode()))
					throw new ArgumentException(string.Format("Unable to register the DependencyExternal '{0}' on type '{1}'. A DependencyExternal with the same Name and OwnerType has already been registered.", name, ownerType));
				registered.Add(np.GetHashCode(), np);
			}
			finally
			{
				reglock.ReleaseReaderLock();
			}
			return np;
		}

		public static DependencyExternal<TType> RegisterExternal<TType>(string name, Type ownerType, TType defaultValue)
		{
			var np = new DependencyExternal<TType>(name, ownerType, defaultValue);
			reglock.AcquireWriterLock(0);
			try
			{
				if (registered.ContainsKey(np.GetHashCode()))
					throw new ArgumentException(string.Format("Unable to register the DependencyExternal '{0}' on type '{1}'. A DependencyExternal with the same Name and OwnerType has already been registered.", name, ownerType));
				registered.Add(np.GetHashCode(), np);
			}
			finally
			{
				reglock.ReleaseReaderLock();
			}
			return np;
		}

		public static DependencyExternal<TType> RegisterExternal<TType>(string name, Type ownerType, TType defaultValue, Action<ExternalPropertyChangedArgs<TType>> ExternalPropertyChangedCallback)
		{
			var np = new DependencyExternal<TType>(name, ownerType, defaultValue, ExternalPropertyChangedCallback);
			reglock.AcquireWriterLock(0);
			try
			{
				if (registered.ContainsKey(np.GetHashCode()))
					throw new ArgumentException(string.Format("Unable to register the DependencyExternal '{0}' on type '{1}'. A DependencyExternal with the same Name and OwnerType has already been registered.", name, ownerType));
				registered.Add(np.GetHashCode(), np);
			}
			finally
			{
				reglock.ReleaseReaderLock();
			}
			return np;
		}

		public static DependencyExternal<TType> RegisterExternal<TType>(string name, Type ownerType, TType defaultValue, Action<ExternalPropertyChangedArgs<TType>> ExternalPropertyChangedCallback, Action<ExternalValidateValueArgs<TType>> ExternalValidateValueCallback)
		{
			var np = new DependencyExternal<TType>(name, ownerType, defaultValue, ExternalPropertyChangedCallback, ExternalValidateValueCallback);
			reglock.AcquireWriterLock(0);
			try
			{
				if (registered.ContainsKey(np.GetHashCode()))
					throw new ArgumentException(string.Format("Unable to register the DependencyExternal '{0}' on type '{1}'. A DependencyExternal with the same Name and OwnerType has already been registered.", name, ownerType));
				registered.Add(np.GetHashCode(), np);
			}
			finally
			{
				reglock.ReleaseReaderLock();
			}
			return np;
		}

		public override int GetHashCode()
		{
			return Name.GetHashCode() ^ OwnerType.GetHashCode();
		}
	}

	public sealed class ExternalPropertyChangedArgs<T>
	{
		public DependencyObjectEx Owner { get; private set; }
		public T NewValue { get; private set; }
		public T OldValue { get; private set; }

		public ExternalPropertyChangedArgs(DependencyObjectEx Owner, T NewValue, T OldValue)
		{
			this.Owner = Owner;
			this.NewValue = NewValue;
			this.OldValue = OldValue;
		}
	}

	public sealed class ExternalValidateValueArgs<T>
	{
		public DependencyObjectEx Owner { get; private set; }
		public T NewValue { get; private set; }
		public bool IsValid { get; set; }

		public ExternalValidateValueArgs(DependencyObjectEx Owner, T NewValue)
		{
			this.Owner = Owner;
			this.NewValue = NewValue;
		}
	}
}