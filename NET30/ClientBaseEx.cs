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
using System.ServiceModel;

namespace NETPath.Toolkit.NET30
{
	public class ClientBaseEx<T> : ClientBase<T> where T : class
	{
		public class AsyncOperationCompletedArgs<TResult>
		{
			private TResult result;

			public TResult Result
			{
				get { return result; }
				private set { result = value; }
			}

			private System.Exception error;

			public System.Exception Error
			{
				get { return error; }
				private set { error = value; }
			}

			private bool cancelled;

			public bool Cancelled
			{
				get { return cancelled; }
				private set { cancelled = value; }
			}

			private object userState;

			public object UserState
			{
				get { return userState; }
				private set { userState = value; }
			}

			public AsyncOperationCompletedArgs(TResult result, Exception error, bool cancelled, Object userState)
			{
				Result = result;
				Error = error;
				Cancelled = cancelled;
				UserState = userState;
			}
		}

		protected new class InvokeAsyncCompletedEventArgs : System.ComponentModel.AsyncCompletedEventArgs
		{
			public object[] Results { get; set; }

			public InvokeAsyncCompletedEventArgs(object[] results, System.Exception error, bool cancelled, Object userState)
				: base(error, cancelled, userState)
			{
				Results = results;
			}
		}

		protected new delegate IAsyncResult BeginOperationDelegate(object[] inValues, AsyncCallback asyncCallback, Object state);
		protected new delegate object[] EndOperationDelegate(IAsyncResult result);

		protected void InvokeAsync(BeginOperationDelegate beginOperationDelegate, object[] inValues, EndOperationDelegate endOperationDelegate, System.Threading.SendOrPostCallback operationCompletedCallback, object userState)
		{
			if (beginOperationDelegate == null)throw new ArgumentNullException("Argument 'beginOperationDelegate' cannot be null.");
			if (endOperationDelegate == null) throw new ArgumentNullException("Argument 'endOperationDelegate' cannot be null.");
			AsyncCallback cb = delegate(IAsyncResult ar)
				                   {
					                   object[] results = null;
					                   Exception error = null;
					                   try
					                   {
						                   results = endOperationDelegate(ar);
					                   }
					                   catch (Exception ex)
					                   {
						                   error = ex;
					                   }
					                   if (operationCompletedCallback != null)
						                   operationCompletedCallback(new InvokeAsyncCompletedEventArgs(results, error, false, userState));
				                   };
			beginOperationDelegate(inValues, cb, userState);
		}

	}
}