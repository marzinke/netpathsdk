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
using System.Collections.Concurrent;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Serialization;

namespace System
{
	[Serializable]
	[DataContract]
	public abstract class DREObjectBase
	{
		//EntityFramework Support
		[NonSerialized, IgnoreDataMember, XmlIgnore] protected static readonly SynchronizedCollection<Action> efactions;
		[NonSerialized, IgnoreDataMember, XmlIgnore] private static Task eftask;
		[NonSerialized, IgnoreDataMember, XmlIgnore] private static readonly CancellationTokenSource eftaskct = new CancellationTokenSource();
		[NonSerialized, IgnoreDataMember, XmlIgnore] private static int updateInterval = 1000;
		[IgnoreDataMember, XmlIgnore] public static int EFUpdateInterval { get { return updateInterval; } set { Interlocked.Exchange(ref updateInterval, value); } }
		[NonSerialized, IgnoreDataMember, XmlIgnore] private ConcurrentQueue<CMDItemBase> efchanges;

		static DREObjectBase()
		{
			efactions = new SynchronizedCollection<Action>();
		}

		public static void StartEF()
		{
			eftask = Task.Factory.StartNew(() =>
			{
				while (!eftaskct.Token.IsCancellationRequested)
				{
					Thread.Sleep(EFUpdateInterval);
					foreach (var efa in efactions) efa();
				}
				eftaskct.Token.ThrowIfCancellationRequested();
			}, eftaskct.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
		}

		public static void StopEF()
		{
			eftaskct.Cancel();
			try { eftask.Wait(); }
			catch (AggregateException ex) { ex.Flatten(); }
		}

		public IEnumerable<CMDItemBase> GetEFChanges()
		{
			var til = new List<CMDItemBase>();
			CMDItemBase t;
			while (efchanges.TryDequeue(out t))
				if (t != null) til.Add(t);
			return til.GroupBy(a => a.Key).Select(a => a.Last()).ToArray();
		}
	
		[NonSerialized, IgnoreDataMember, XmlIgnore] private ConcurrentDictionary<HashID, object> values;
		[NonSerialized, IgnoreDataMember, XmlIgnore] private ConcurrentQueue<CMDItemBase> modifications;
		[NonSerialized, IgnoreDataMember, XmlIgnore] private long changeCount;
		[IgnoreDataMember, XmlIgnore] protected long ChangeCount { get { return changeCount; } }
		[NonSerialized, IgnoreDataMember, XmlIgnore] private long batchInterval;
		[IgnoreDataMember, XmlIgnore] public long BatchInterval { get { return batchInterval; } protected set { Interlocked.Exchange(ref batchInterval, value); } }
		[NonSerialized, IgnoreDataMember, XmlIgnore] private DependencyObjectEx baseXAMLObject; 
		[IgnoreDataMember, XmlIgnore] protected DependencyObjectEx BaseXAMLObject { get { return baseXAMLObject; } set { if (baseXAMLObject == null) baseXAMLObject = value; } }
		[NonSerialized, IgnoreDataMember, XmlIgnore] private int isDirty = 0;
		[IgnoreDataMember, XmlIgnore] public bool IsDirty { get { if (isDirty == 0) { return false; } return true; } internal set { if (value) { Interlocked.Exchange(ref isDirty, 1); } else { Interlocked.Exchange(ref isDirty, 0); } } }

		protected DREObjectBase()
		{
			modifications = new ConcurrentQueue<CMDItemBase>();
			efchanges = new ConcurrentQueue<CMDItemBase>();
			values = new ConcurrentDictionary<HashID, object>();
			changeCount = 0;
			BatchInterval = 0;
			baseXAMLObject = null;
		}

		protected DREObjectBase(DependencyObjectEx baseXAMLObject)
		{
			modifications = new ConcurrentQueue<CMDItemBase>();
			efchanges = new ConcurrentQueue<CMDItemBase>();
			values = new ConcurrentDictionary<HashID, object>();
			changeCount = 0;
			BatchInterval = 0;
			this.baseXAMLObject = baseXAMLObject;
		}

		protected DREObjectBase(long BatchInterval)
		{
			modifications = new ConcurrentQueue<CMDItemBase>();
			efchanges = new ConcurrentQueue<CMDItemBase>();
			values = new ConcurrentDictionary<HashID, object>();
			changeCount = 0;
			this.BatchInterval = BatchInterval;
			baseXAMLObject = null;
		}

		protected DREObjectBase(DependencyObjectEx baseXAMLObject, long BatchInterval)
		{
			modifications = new ConcurrentQueue<CMDItemBase>();
			efchanges = new ConcurrentQueue<CMDItemBase>();
			values = new ConcurrentDictionary<HashID, object>();
			changeCount = 0;
			this.BatchInterval = BatchInterval;
			this.baseXAMLObject = baseXAMLObject;
		}

		public T GetValue<T>(DREProperty<T> de)
		{
			object value;
			return values.TryGetValue(de.ID, out value) == false ? de.DefaultValue : (T)value;
		}

		internal object GetValue(CMDPropertyBase de)
		{
			object value;
			return values.TryGetValue(de.ID, out value) == false ? de.defaultValue : value;
		}

		public void SetValue<T>(DREProperty<T> de, T value)
		{
			//Call the validator to see if this value is acceptable
			if (de.DeltaValidateValueCallback != null && !de.DeltaValidateValueCallback(this, value)) return;

			//If the new value is the default value remove this from the modified values list, otherwise add/update it.
			if (EqualityComparer<T>.Default.Equals(value, de.DefaultValue))
			{
				//Remove the value from the list, which sets it to the default value.
				object temp;
				if (!values.TryRemove(de.ID, out temp)) return;
				if (de.EnableEF) efchanges.Enqueue(new CMDItemValue<T>(true, de.ID));
				IsDirty = true;
				if (de.EnableBatching && BatchInterval > 0)
				{
					modifications.Enqueue(new CMDItemValue<T>(true, de.ID));
					IncrementChangeCount();
				}

				if (de.XAMLProperty != null && baseXAMLObject != null) baseXAMLObject.UpdateValueThreaded(de.XAMLProperty, de.defaultValue);
				if (de.XAMLPropertyKey != null && baseXAMLObject != null) baseXAMLObject.UpdateValueThreaded(de.XAMLPropertyKey, de.defaultValue);

				//Call the property updated callback
				if (temp != null && de.DREPropertyUpdatedCallback != null && baseXAMLObject != null) de.DREPropertyUpdatedCallback(this, (T)temp, de.DefaultValue);

				//Call the property changed callback
				if (temp != null && de.DREPropertyChangedCallback != null && baseXAMLObject != null) de.DREPropertyChangedCallback(this, (T)temp, de.DefaultValue);
			}
			else
			{
				//Update the value
				object temp = values.AddOrUpdate(de.ID, value, (p, v) => value);
				IsDirty = true;
				if (de.EnableBatching && BatchInterval > 0)
				{
					modifications.Enqueue(new CMDItemValue<T>(false, de.ID, value));
					IncrementChangeCount();
				}

				if (de.XAMLProperty != null && baseXAMLObject != null) baseXAMLObject.UpdateValueThreaded(de.XAMLProperty, value);
				if (de.XAMLPropertyKey != null && baseXAMLObject != null) baseXAMLObject.UpdateValueThreaded(de.XAMLPropertyKey, value);

				//Call the property updated callback
				if (temp != null && de.DREPropertyUpdatedCallback != null && baseXAMLObject != null) de.DREPropertyUpdatedCallback(this, (T)temp, value);

				//Call the property changed callback
				if (temp != null && de.DREPropertyChangedCallback != null && baseXAMLObject != null) de.DREPropertyChangedCallback(this, (T)temp, value);
			}
		}

		public void UpdateValueExternal<T>(DREProperty<T> de, T value)
		{
			//If the new value is the default value remove this from the modified values list, otherwise add/update it.
			if (Equals(value, de.DefaultValue))
			{
				//Remove the value from the list, which sets it to the default value.
				object temp;
				if (!values.TryRemove(de.ID, out temp)) return;
				if (de.EnableEF) efchanges.Enqueue(new CMDItemValue<T>(true, de.ID));
				IsDirty = true;
			}
			else
			{
				//Update the values
				var temp = (T)values.AddOrUpdate(de.ID, value, (p, v) => value);
				if (de.EnableEF) efchanges.Enqueue(new CMDItemValue<T>(false, de.ID, value));
				IsDirty = true;
			}
			if (de.XAMLProperty != null && baseXAMLObject != null) baseXAMLObject.UpdateValueThreaded(de.XAMLProperty, value);
			if (de.XAMLPropertyKey != null && baseXAMLObject != null) baseXAMLObject.UpdateValueThreaded(de.XAMLPropertyKey, value);
		}

		public void UpdateValueInternal<T>(DREProperty<T> de, T value)
		{
			//If the new value is the default value remove this from the modified values list, otherwise add/update it.
			if (Equals(value, de.DefaultValue))
			{
				//Remove the value from the list, which sets it to the default value.
				object temp;
				if (!values.TryRemove(de.ID, out temp)) return;
				if (de.EnableEF) efchanges.Enqueue(new CMDItemValue<T>(true, de.ID));
				IsDirty = true;
				if (de.EnableBatching && BatchInterval > 0)
				{
					modifications.Enqueue(new CMDItemValue<T>(true, de.ID));
					IncrementChangeCount();
				}

				//Call the property updated callback
				if (temp != null && de.DREPropertyUpdatedCallback != null && baseXAMLObject != null) de.DREPropertyUpdatedCallback(this, (T)temp, value);
			}
			else
			{
				//Update the values
				var temp = (T)values.AddOrUpdate(de.ID, value, (p, v) => value);
				if (de.EnableEF) efchanges.Enqueue(new CMDItemValue<T>(false, de.ID, value));
				IsDirty = true;
				if (de.EnableBatching && BatchInterval > 0)
				{
					modifications.Enqueue(new CMDItemValue<T>(false, de.ID, value));
					IncrementChangeCount();
				}

				//Call the property updated callback
				if (temp != null && de.DREPropertyUpdatedCallback != null && baseXAMLObject != null) de.DREPropertyUpdatedCallback(this, (T)temp, value);
			}
		}

		public void ClearValue<T>(DREProperty<T> de)
		{
			object temp;
			if (!values.TryRemove(de.ID, out temp))
			{
				if (de.EnableEF) efchanges.Enqueue(new CMDItemValue<T>(true, de.ID));
				IsDirty = true;
				if (de.EnableBatching && BatchInterval > 0)
				{
					modifications.Enqueue(new CMDItemValue<T>(true, de.ID));
					IncrementChangeCount();
				}
			}
			if (de.DREPropertyChangedCallback != null)
				de.DREPropertyChangedCallback(this, (T) temp, de.DefaultValue);
		}

		public void ApplyDelta<T>(CMDItemValue<T> v)
		{
			if (v == null) return;
			if (v.UseDefault)
			{
				object temp;
				values.TryRemove(v.Key, out temp);
				IsDirty = true;
				var de = CMDPropertyBase.FromID(v.Key) as DREProperty<T>;
				if (de != null && de.XAMLProperty != null && baseXAMLObject != null) baseXAMLObject.UpdateValueThreaded(de.XAMLProperty, de.DefaultValue);
				if (de != null && de.XAMLPropertyKey != null && baseXAMLObject != null) baseXAMLObject.UpdateValueThreaded(de.XAMLPropertyKey, de.DefaultValue);
			}
			else
			{
				var temp = values.AddOrUpdate(v.Key, v.Value, (p, a) => v.Value);
				IsDirty = true;
				var de = CMDPropertyBase.FromID(v.Key) as DREProperty<T>;
				if (de != null && de.XAMLProperty != null && baseXAMLObject != null) baseXAMLObject.UpdateValueThreaded(de.XAMLProperty, v.Value);
				if (de != null && de.XAMLPropertyKey != null && baseXAMLObject != null) baseXAMLObject.UpdateValueThreaded(de.XAMLPropertyKey, v.Value);
			}
		}

		public Dictionary<HashID, object> GetNonDefaultValues()
		{
			return values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
		}

		public IEnumerable<CMDItemBase> GetDeltaValues()
		{
			var til = new List<CMDItemBase>();
			CMDItemBase t;
			while (modifications.TryDequeue(out t))
				if (t != null) til.Add(t);
			return til.GroupBy(a => a.Key).Select(a => a.Last()).ToArray();
		}

		protected virtual void IncrementChangeCount()
		{
			if (ChangeCount >= (BatchInterval - 1)) BatchUpdates();

			//If the change notification interval is less than zero, do nothing.
			if (BatchInterval < 1) return;
			Threading.Interlocked.Increment(ref changeCount);

			//If the change count is greater than the interval run the batch updates.
			//Note that we don't need to use CompareExchange here because we only care if the value is greater-than-or-equal-to the batch interval, not what the exact overage is.
			if (ChangeCount < BatchInterval) return;
			Threading.Interlocked.Exchange(ref changeCount, 0);
		}

		protected virtual void OnDeserializingBase(StreamingContext context)
		{
			modifications = new ConcurrentQueue<CMDItemBase>();
			efchanges = new ConcurrentQueue<CMDItemBase>();
			values = new ConcurrentDictionary<HashID, object>();
			changeCount = 0;
			BatchInterval = 0;
		}

		[DataMember] public Guid _DREID { get; set; }

		protected void SetDREID(string PrimaryKey) { if (_DREID == Guid.Empty) _DREID = HashID.GenerateHashID(PrimaryKey).ToGUID(); }
		protected void SetDREID(byte[] PrimaryKey) { if (_DREID == Guid.Empty) _DREID = HashID.GenerateHashID(PrimaryKey).ToGUID(); }
		protected void SetDREID(byte PrimaryKey) { if (_DREID == Guid.Empty) _DREID = HashID.GenerateHashID(new byte[] { PrimaryKey }).ToGUID(); }
		protected void SetDREID(sbyte PrimaryKey) { if (_DREID == Guid.Empty) _DREID = HashID.GenerateHashID(BitConverter.GetBytes(PrimaryKey)).ToGUID(); }
		protected void SetDREID(short PrimaryKey) { if (_DREID == Guid.Empty) _DREID = HashID.GenerateHashID(BitConverter.GetBytes(PrimaryKey)).ToGUID(); }
		protected void SetDREID(int PrimaryKey) { if (_DREID == Guid.Empty) _DREID = HashID.GenerateHashID(BitConverter.GetBytes(PrimaryKey)).ToGUID(); }
		protected void SetDREID(long PrimaryKey) { if (_DREID == Guid.Empty) _DREID = HashID.GenerateHashID(BitConverter.GetBytes(PrimaryKey)).ToGUID(); }
		protected void SetDREID(ushort PrimaryKey) { if (_DREID == Guid.Empty) _DREID = HashID.GenerateHashID(BitConverter.GetBytes(PrimaryKey)).ToGUID(); }
		protected void SetDREID(uint PrimaryKey) { if (_DREID == Guid.Empty) _DREID = HashID.GenerateHashID(BitConverter.GetBytes(PrimaryKey)).ToGUID(); }
		protected void SetDREID(ulong PrimaryKey) { if (_DREID == Guid.Empty) _DREID = HashID.GenerateHashID(BitConverter.GetBytes(PrimaryKey)).ToGUID(); }
		protected void SetDREID(Guid PrimaryKey) { if (_DREID == Guid.Empty) _DREID = HashID.GenerateHashID(PrimaryKey.ToByteArray()).ToGUID(); }

		protected abstract void BatchUpdates();
	}

	[DataContract]
	public abstract class DREObject<T> : DREObjectBase where T : DREObject<T>
	{
		[NonSerialized, IgnoreDataMember, XmlIgnore] private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, T> __dcm;
		static DREObject()
		{
			__dcm = new System.Collections.Concurrent.ConcurrentDictionary<Guid, T>();
		}

		public static T GetDataFromID(Guid ID)
		{
			T t;
			__dcm.TryGetValue(ID, out t);
			return t;
		}

		public static void UpdateValue<TType>(Guid ID, DREProperty<TType> prop, TType value)
		{
			T t;
			__dcm.TryGetValue(ID, out t);
			if (t != null) t.UpdateValueExternal(prop, value);
		}

		public static bool HasData(Guid DataID)
		{
			return __dcm.ContainsKey(DataID);
		}

		public static T Register(Guid ClientID, T Data)
		{
			Data.__crl.GetOrAdd(ClientID, Data._DREID);
			return __dcm.GetOrAdd(Data._DREID, Data);
		}

		public static T Register(Guid ClientID, Guid DataID)
		{
			T Data;
			__dcm.TryGetValue(DataID, out Data);
			if (Data == null) return null;
			Data.__crl.GetOrAdd(ClientID, Data._DREID);
			return __dcm.GetOrAdd(Data._DREID, Data);
		}

		public static T Unregister(Guid ClientID, Guid DataID)
		{
			T data;
			__dcm.TryGetValue(DataID, out data);
			if (data == null) return null;
			Guid dreid;
			data.__crl.TryRemove(ClientID, out dreid);
			__dcm.TryRemove(DataID, out data);
			return data;
		}

		//Constructors

		protected DREObject()
		{
			_DREID = Guid.Empty;
			__crl = new ConcurrentDictionary<Guid, Guid>();
		}

		protected DREObject(DependencyObjectEx baseXAMLObject)
			: base(baseXAMLObject)
		{
			_DREID = Guid.Empty;
			__crl = new ConcurrentDictionary<Guid, Guid>();
		}

		protected DREObject(long BatchInterval)
			: base(BatchInterval)
		{
			_DREID = Guid.Empty;
			__crl = new ConcurrentDictionary<Guid, Guid>();
		}

		protected DREObject(DependencyObjectEx baseXAMLObject, long BatchInterval)
			: base(baseXAMLObject, BatchInterval)
		{
			_DREID = Guid.Empty;
			__crl = new ConcurrentDictionary<Guid, Guid>();
		}

		protected override sealed void OnDeserializingBase(StreamingContext context)
		{
			base.OnDeserializingBase(context);
			__crl = new ConcurrentDictionary<Guid, Guid>();
		}

		[IgnoreDataMember, XmlIgnore] public IEnumerable<Guid> ClientList { get { return __crl.Keys; } }
		[NonSerialized, IgnoreDataMember, XmlIgnore] private ConcurrentDictionary<Guid, Guid> __crl = new ConcurrentDictionary<Guid, Guid>();
	}


	[DataContract]
	public abstract class EFDREObject<T, TDataContext> : DREObject<T> where T : EFDREObject<T, TDataContext> where TDataContext : DbContext, new()
	{
		[NonSerialized, IgnoreDataMember, XmlIgnore] private static readonly ConcurrentDictionary<Guid, T> efobjects;
		[NonSerialized, IgnoreDataMember, XmlIgnore] private static string efconnection;
		[IgnoreDataMember, XmlIgnore] public static string EFConnection { get { return efconnection; } set { Interlocked.Exchange(ref efconnection, value); } }

		static EFDREObject()
		{
			efobjects = new ConcurrentDictionary<Guid, T>();
			efactions.Add(DoEFUpdates);
		}

		private static void DoEFUpdates()
		{
			var db = new TDataContext();
			if (!string.IsNullOrEmpty(efconnection)) db.Database.Connection.ConnectionString = efconnection;
			db.Database.Connection.Open();

			var efol = efobjects.ToArray().Where(a => a.Value.IsDirty).Select(b => b.Value).ToList();
			foreach (var efo in efol) efo.UpdateDataObject(db);

			db.SaveChanges();
			db.Database.Connection.Close();
		}

		protected EFDREObject()
		{
		}

		protected EFDREObject(string Connection)
		{
			EFConnection = Connection;
		}

		protected EFDREObject(string Connection, DependencyObjectEx baseXAMLObject)
			: base(baseXAMLObject)
		{
			EFConnection = Connection;
		}

		protected EFDREObject(string Connection, long BatchInterval)
			: base(BatchInterval)
		{
			EFConnection = Connection;
		}

		protected EFDREObject(string Connection, DependencyObjectEx baseXAMLObject, long BatchInterval)
			: base(baseXAMLObject, BatchInterval)
		{
			EFConnection = Connection;
		}

		public T Register()
		{
			return (T)efobjects.GetOrAdd(_DREID, (T)this);
		}

		public T Unregister()
		{
			T temp;
			efobjects.TryRemove(_DREID, out temp);
			return temp;
		}

		public void ExecuteEF(Action<TDataContext> execute)
		{
			var db = new TDataContext();
			if (!string.IsNullOrEmpty(efconnection)) db.Database.Connection.ConnectionString = efconnection;
			db.Database.Connection.Open();

			execute(db);

			db.SaveChanges();
			db.Database.Connection.Close();
		}

		protected abstract void UpdateDataObject(TDataContext Database);
	}

}