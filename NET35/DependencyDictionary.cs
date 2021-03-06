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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace System.Collections.Generic
{
	class DependencyDictionary<TKey, TValue> : IDictionary<TKey, TValue>
	{
		private readonly Dictionary<TKey, TValue> il;
		private readonly ReaderWriterLockSlim ocl;
		private readonly Action<KeyValuePair<TKey, TValue>> Added;
		private readonly Action<KeyValuePair<TKey, TValue>> Removed;
		private readonly Action<int> Cleared;
		private readonly Action<TKey, TValue, TValue> Updated;

		~DependencyDictionary()
		{
			try
			{
				ocl.Dispose();
			}
			catch { }
		}

		public DependencyDictionary()
		{
			il = new Dictionary<TKey, TValue>();
			ocl = new ReaderWriterLockSlim();
			Added = (items => { });
			Removed = (items => { });
			Cleared = (count => { });
			Updated = ((key, ov, nv) => { });
		}

		public DependencyDictionary(int Capacity)
		{
			il = new Dictionary<TKey, TValue>(Capacity);
			ocl = new ReaderWriterLockSlim();
			Added = (items => { });
			Removed = (items => { });
			Cleared = (count => { });
			Updated = ((key, ov, nv) => { });
		}

		public DependencyDictionary(IDictionary<TKey, TValue> Items)
		{
			il = new Dictionary<TKey, TValue>(Items);
			ocl = new ReaderWriterLockSlim();
			Added = (items => { });
			Removed = (items => { });
			Cleared = (count => { });
			Updated = ((key, ov, nv) => { });
		}

		public DependencyDictionary(Action<KeyValuePair<TKey, TValue>> Added, Action<KeyValuePair<TKey, TValue>> Removed, Action<int> Cleared, Action<TKey, TValue, TValue> Updated)
		{
			il = new Dictionary<TKey, TValue>();
			ocl = new ReaderWriterLockSlim();
			this.Added = Added ?? (items => { });
			this.Removed = Removed ?? (items => { });
			this.Cleared = Cleared ?? (count => { });
			this.Updated = Updated ?? ((key, ov, nv) => { });
		}

		public DependencyDictionary(int Capacity, Action<KeyValuePair<TKey, TValue>> Added, Action<KeyValuePair<TKey, TValue>> Removed, Action<int> Cleared, Action<TKey, TValue, TValue> Updated)
		{
			il = new Dictionary<TKey, TValue>(Capacity);
			ocl = new ReaderWriterLockSlim();
			this.Added = Added ?? (items => { });
			this.Removed = Removed ?? (items => { });
			this.Cleared = Cleared ?? (count => { });
			this.Updated = Updated ?? ((key, ov, nv) => { });
		}

		public DependencyDictionary(IDictionary<TKey, TValue> Items, Action<KeyValuePair<TKey, TValue>> Added, Action<KeyValuePair<TKey, TValue>> Removed, Action<int> Cleared, Action<TKey, TValue, TValue> Updated)
		{
			il = new Dictionary<TKey, TValue>(Items);
			ocl = new ReaderWriterLockSlim();
			this.Added = Added ?? (items => { });
			this.Removed = Removed ?? (items => { });
			this.Cleared = Cleared ?? (count => { });
			this.Updated = Updated ?? ((key, ov, nv) => { });
		}

		public void Add(TKey key, TValue value)
		{
			ocl.EnterWriteLock();
			try
			{
				il.Add(key, value);
			}
			finally
			{
				ocl.ExitWriteLock();
			}
			CallAdded(key, value);
		}

		public bool ContainsKey(TKey key)
		{
			ocl.EnterReadLock();
			try
			{
				return il.ContainsKey(key);
			}
			finally
			{
				ocl.ExitReadLock();
			}
		}

		public bool ContainsValue(TValue value)
		{
			ocl.EnterReadLock();
			try
			{
				return il.ContainsValue(value);
			}
			finally 
			{
				ocl.ExitReadLock();
			}
		}

		public ICollection<TKey> Keys
		{
			get
			{
				ocl.EnterReadLock();
				try
				{
					return il.Keys;
				}
				finally
				{
					ocl.ExitReadLock();
				}
			}
		}

		public bool Remove(TKey key)
		{
			TKey k = key;
			TValue v = il[key];
			ocl.EnterWriteLock();
			bool rt;
			try
			{
				rt = il.Remove(key);
			}
			finally
			{
				ocl.ExitWriteLock();
			}
			CallRemoved(k, v);
			return rt;
		}

		public bool TryGetValue(TKey key, out TValue value)
		{
			ocl.EnterReadLock();
			try
			{
				return il.TryGetValue(key, out value);
			}
			finally
			{
				ocl.ExitReadLock();
			}
		}

		public ICollection<TValue> Values
		{
			get
			{
				ocl.EnterReadLock();
				try
				{
					return il.Values;
				}
				finally
				{
					ocl.ExitReadLock();
				}
			}
		}

		public TValue this[TKey key]
		{
			get
			{
				ocl.EnterReadLock();
				try
				{
					return il[key];
				}
				finally
				{
					ocl.ExitReadLock();
				}
			}
			set
			{
				TValue ov = il[key];
				ocl.EnterWriteLock();
				try
				{
					il[key] = value;
				}
				finally
				{
					ocl.ExitWriteLock();
				}
				CallUpdated(key, ov, value);
			}
		}

		public void Clear()
		{
			int c = Count;
			ocl.EnterWriteLock();
			try
			{
				il.Clear();
			}
			finally
			{
				ocl.ExitWriteLock();
			}
			CallCleared(c);
		}

		public int Count
		{
			get
			{
				ocl.EnterReadLock();
				try
				{
					return il.Count;
				}
				finally 
				{
					ocl.ExitReadLock();
				}
			}
		}

		public bool IsReadOnly
		{
			get { return false; }
		}

		private void CallAdded(TKey key, TValue value)
		{
			if (Application.Current.Dispatcher == null) { Added(new KeyValuePair<TKey, TValue>(key, value)); return; }
			if (Application.Current.Dispatcher.CheckAccess()) Added(new KeyValuePair<TKey, TValue>(key, value));
			else Application.Current.Dispatcher.Invoke(new Action(() => Added(new KeyValuePair<TKey, TValue>(key, value))), DispatcherPriority.Normal);
		}

		private void CallRemoved(TKey key, TValue value)
		{
			if (Application.Current.Dispatcher == null) { Removed(new KeyValuePair<TKey, TValue>(key, value)); return; }
			if (Application.Current.Dispatcher.CheckAccess()) Removed(new KeyValuePair<TKey, TValue>(key, value));
			else Application.Current.Dispatcher.Invoke(new Action(() => Removed(new KeyValuePair<TKey, TValue>(key, value))), DispatcherPriority.Normal);
		}

		private void CallCleared(int count)
		{
			if (Application.Current.Dispatcher == null) { Cleared(count); return; }
			if (Application.Current.Dispatcher.CheckAccess()) Cleared(count);
			else Application.Current.Dispatcher.Invoke(new Action(() => Cleared(count)), DispatcherPriority.Normal);
		}

		private void CallUpdated(TKey index, TValue olditem, TValue newitem)
		{
			if (Application.Current.Dispatcher == null) { Updated(index, olditem, newitem); return; }
			if (Application.Current.Dispatcher.CheckAccess()) Updated(index, olditem, newitem);
			else Application.Current.Dispatcher.Invoke(new Action(() => Updated(index, olditem, newitem)), DispatcherPriority.Normal);
		}

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			ocl.EnterReadLock();
			try
			{
				return il.GetEnumerator();
			}
			finally
			{
				ocl.ExitReadLock();
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			ocl.EnterReadLock();
			try
			{
				return ((IEnumerable)il).GetEnumerator();
			}
			finally
			{
				ocl.ExitReadLock();
			}
		}

		void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
		{
			Add(item.Key, item.Value);
		}

		bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
		{
			return ContainsKey(item.Key);
		}

		void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			ocl.EnterReadLock();
			try
			{
				((ICollection<KeyValuePair<TKey, TValue>>)il).CopyTo(array, arrayIndex);
			}
			finally
			{
				ocl.ExitReadLock();
			}
		}

		bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
		{
			return Remove(item.Key);
		}
	}
}