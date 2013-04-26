﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace System.ServiceModel
{
	public abstract class RESTClientBase
	{
		public static CookieContainer GlobalCookies { get; private set; }
		static RESTClientBase()
		{
			GlobalCookies = new CookieContainer();
		}

		public Uri BaseURI { get; protected set; }
		public CookieContainer Cookies { get; private set; }
		public CredentialCache CredentialCache { get; protected set; }
		public NetworkCredential Credentials { get; protected set; }
		public IWebProxy Proxy { get; protected set; }

		protected RESTClientBase()
		{
			Cookies = new CookieContainer();
			Credentials = null;
		}

		protected RESTClientBase(string BaseURI, CookieContainer Cookies = null, NetworkCredential Credentials = null, CredentialCache CredentialCache = null, IWebProxy Proxy = null)
		{
			this.BaseURI = new Uri(BaseURI);
			this.Cookies = Cookies ?? new CookieContainer();
			this.Credentials = Credentials;
			this.CredentialCache = CredentialCache;
			this.Proxy = Proxy;
		}

		protected string ParseURI(string Uri, IDictionary<string, object> Parameters)
		{
			string t = Uri;
			foreach (KeyValuePair<string, object> kvp in Parameters)
				t = Regex.Replace(t, string.Format("\\{{{0}\\}}", kvp.Key), kvp.Value.ToString(), RegexOptions.IgnoreCase);
			return t;
		}
	}

	public sealed class RESTHTTPWebConfig
	{
		public CookieContainer CookieContainer { get; set; }
		public WebHeaderCollection Headers { get; private set; }
		public WebProxy Proxy { get; set; }
		public X509CertificateCollection ClientCertificates { get; set; }
		public NetworkCredential NetworkCredential { get; set; }
		public CredentialCache CredentialCache { get; set; }

		public HttpContinueDelegate Continuation { get; set; }

		public string Accept { get { return Headers[HttpRequestHeader.Accept]; } set { Headers[HttpRequestHeader.Accept] = value; } }
		public string AcceptCharset { get { return Headers[HttpRequestHeader.AcceptCharset]; } set { Headers[HttpRequestHeader.AcceptCharset] = value; } }
		public string AcceptEncoding { get { return Headers[HttpRequestHeader.AcceptEncoding]; } set { Headers[HttpRequestHeader.AcceptEncoding] = value; } }
		public string AcceptLanguage { get { return Headers[HttpRequestHeader.AcceptLanguage]; } set { Headers[HttpRequestHeader.AcceptLanguage] = value; } }
		public string Allow { get { return Headers[HttpRequestHeader.Allow]; } set { Headers[HttpRequestHeader.Allow] = value; } }
		public string Authorization { get { return Headers[HttpRequestHeader.Authorization]; } set { Headers[HttpRequestHeader.Authorization] = value; } }
		public string CacheControl { get { return Headers[HttpRequestHeader.CacheControl]; } set { Headers[HttpRequestHeader.CacheControl] = value; } }
		public string Connection { get { return Headers[HttpRequestHeader.Connection]; } set { Headers[HttpRequestHeader.Connection] = value; } }
		public string ContentLength { get { return Headers[HttpRequestHeader.ContentLength]; } set { Headers[HttpRequestHeader.ContentLength] = value; } }
		public string ContentEncoding { get { return Headers[HttpRequestHeader.ContentEncoding]; } set { Headers[HttpRequestHeader.ContentEncoding] = value; } }
		public string ContentLanguage { get { return Headers[HttpRequestHeader.ContentLanguage]; } set { Headers[HttpRequestHeader.ContentLanguage] = value; } }
		public string ContentLocation { get { return Headers[HttpRequestHeader.ContentLocation]; } set { Headers[HttpRequestHeader.ContentLocation] = value; } }
		public string ContentMd5 { get { return Headers[HttpRequestHeader.ContentMd5]; } set { Headers[HttpRequestHeader.ContentMd5] = value; } }
		public string ContentRange { get { return Headers[HttpRequestHeader.ContentRange]; } set { Headers[HttpRequestHeader.ContentRange] = value; } }
		public string ContentType { get { return Headers[HttpRequestHeader.ContentType]; } set { Headers[HttpRequestHeader.ContentType] = value; } }
		public string Cookie { get { return Headers[HttpRequestHeader.Cookie]; } set { Headers[HttpRequestHeader.Cookie] = value; } }
		public string Date { get { return Headers[HttpRequestHeader.Date]; } set { Headers[HttpRequestHeader.Date] = value; } }
		public string Expires { get { return Headers[HttpRequestHeader.Expires]; } set { Headers[HttpRequestHeader.Expires] = value; } }
		public string Expect { get { return Headers[HttpRequestHeader.Expect]; } set { Headers[HttpRequestHeader.Expect] = value; } }
		public string From { get { return Headers[HttpRequestHeader.From]; } set { Headers[HttpRequestHeader.From] = value; } }
		public string Host { get { return Headers[HttpRequestHeader.Host]; } set { Headers[HttpRequestHeader.Host] = value; } }
		public string IfMatch { get { return Headers[HttpRequestHeader.IfMatch]; } set { Headers[HttpRequestHeader.IfMatch] = value; } }
		public string IfModifiedSince { get { return Headers[HttpRequestHeader.IfModifiedSince]; } set { Headers[HttpRequestHeader.IfModifiedSince] = value; } }
		public string IfNoneMatch { get { return Headers[HttpRequestHeader.IfNoneMatch]; } set { Headers[HttpRequestHeader.IfNoneMatch] = value; } }
		public string IfRange { get { return Headers[HttpRequestHeader.IfRange]; } set { Headers[HttpRequestHeader.IfRange] = value; } }
		public string IfUnmodifiedSince { get { return Headers[HttpRequestHeader.IfUnmodifiedSince]; } set { Headers[HttpRequestHeader.IfUnmodifiedSince] = value; } }
		public string KeepAlive { get { return Headers[HttpRequestHeader.KeepAlive]; } set { Headers[HttpRequestHeader.KeepAlive] = value; } }
		public string LastModified { get { return Headers[HttpRequestHeader.LastModified]; } set { Headers[HttpRequestHeader.LastModified] = value; } }
		public string MaxForwards { get { return Headers[HttpRequestHeader.MaxForwards]; } set { Headers[HttpRequestHeader.MaxForwards] = value; } }
		public string Pragma { get { return Headers[HttpRequestHeader.Pragma]; } set { Headers[HttpRequestHeader.Pragma] = value; } }
		public string ProxyAuthorization { get { return Headers[HttpRequestHeader.ProxyAuthorization]; } set { Headers[HttpRequestHeader.ProxyAuthorization] = value; } }
		public string Range { get { return Headers[HttpRequestHeader.Range]; } set { Headers[HttpRequestHeader.Range] = value; } }
		public string Referer { get { return Headers[HttpRequestHeader.Referer]; } set { Headers[HttpRequestHeader.Referer] = value; } }
		public string Te { get { return Headers[HttpRequestHeader.Te]; } set { Headers[HttpRequestHeader.Te] = value; } }
		public string Trailer { get { return Headers[HttpRequestHeader.Trailer]; } set { Headers[HttpRequestHeader.Trailer] = value; } }
		public string TransferEncoding { get { return Headers[HttpRequestHeader.TransferEncoding]; } set { Headers[HttpRequestHeader.TransferEncoding] = value; } }
		public string Translate { get { return Headers[HttpRequestHeader.Translate]; } set { Headers[HttpRequestHeader.Translate] = value; } }
		public string UserAgent { get { return Headers[HttpRequestHeader.UserAgent]; } set { Headers[HttpRequestHeader.UserAgent] = value; } }
		public string Upgrade { get { return Headers[HttpRequestHeader.Upgrade]; } set { Headers[HttpRequestHeader.Upgrade] = value; } }
		public string Via { get { return Headers[HttpRequestHeader.Via]; } set { Headers[HttpRequestHeader.Via] = value; } }
		public string Warning { get { return Headers[HttpRequestHeader.Warning]; } set { Headers[HttpRequestHeader.Warning] = value; } }

		public RESTHTTPWebConfig()
		{
			Headers = new WebHeaderCollection();
		}

		public HttpWebRequest CreateRequest(string RequestUri, CookieContainer CookieContainer = null, bool UseHTTP10 = false)
		{
			var t = (HttpWebRequest)WebRequest.Create(new Uri(RequestUri, UriKind.Absolute));

			if (UseHTTP10) t.ProtocolVersion = new Version(1, 0);
			if (NetworkCredential != null) t.Credentials = NetworkCredential;
			if (CredentialCache != null) t.Credentials = CredentialCache;
			t.CookieContainer = this.CookieContainer ?? CookieContainer;
			t.ClientCertificates = ClientCertificates;
			t.Proxy = Proxy;
			t.Headers = Headers;
			t.ContinueDelegate = Continuation;

			return t;
		}
	}
}