using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Text;

namespace Cadenza.Net
{
	public class WebDavMethodBuilder {

		Uri server;
		public Uri Server {
			get {return server;}
			set {
				if (!value.AbsolutePath.EndsWith ("/")) {
					var b = new UriBuilder (value);
					b.Path += "/";
					value = b.Uri;
				}
				server = value;
			}
		}
		public NetworkCredential NetworkCredential {get; set;}
		public IDictionary<string, string> RequestHeaders {get; set;}
		public TextWriter Log {get; set;}
		
		public Task<WebDavPropertyFindMethod> CreateFileStatusMethodAsync (string path = null, int? depth = null, XElement request = null)
		{
			request = request ?? new XElement (WebDavNames.Propfind,
					new XElement (WebDavNames.Prop,
			              new XElement (WebDavNames.CreationDate),
			              new XElement (WebDavNames.ResourceType),
			              new XElement (WebDavNames.GetContentLength)));
			return CreatePropertyFindMethodAsync (path, depth, request);
		}

		public Task<WebDavPropertyFindMethod> CreatePropertyFindMethodAsync (string path = null, int? depth = null, XElement request = null)
		{
			request = request ?? new XElement (WebDavNames.Propfind,
					new XElement (WebDavNames.Propname));
			var uri = CreateUri (path);
			var r   = new WebDavPropertyFindMethod (uri, ToStream (request), depth ?? 1);
			return CreateMethodAsync (WebDavMethods.PropertyFind, uri, r);
		}

		public Task<WebDavDownloadMethod> CreateDownloadMethodAsync (string remotePath, Stream downloadedContents)
		{
			var r = new WebDavDownloadMethod (downloadedContents);
			return CreateMethodAsync (WebRequestMethods.Http.Get, CreateUri (remotePath), r);
		}

		Uri CreateUri (string path)
		{
			path = path ?? "";
			var p = new Uri (path, UriKind.Relative);
			return new Uri (Server, p);
		}

		static Stream ToStream (XElement e)
		{
			return new MemoryStream (Encoding.UTF8.GetBytes (e.ToString ()));
		}

		public Task<TResult> CreateMethodAsync<TResult> (string requestMethod, Uri uri, TResult result)
			where TResult : WebDavMethod
		{
			var request = (HttpWebRequest) HttpWebRequest.Create (uri);

			if (NetworkCredential != null) {
				request.Credentials      = NetworkCredential;
				request.PreAuthenticate  = true;
			}

			request.Method = requestMethod;

			AddHeaders (request.Headers, RequestHeaders);
			AddHeaders (request.Headers, result.RequestHeaders);

			/*
             * The following line fixes an authentication problem explained here:
             * http://www.devnewsgroups.net/dotnetframework/t9525-http-protocol-violation-long.aspx
             */
			System.Net.ServicePointManager.Expect100Continue = false;

			result.Request = request;
			result.Builder = this;

			if (Log != null) {
				Log.WriteLine ("{0} {1}", request.Method, uri.AbsolutePath);
			}

			return Task<TResult>.Factory.StartNew (() => {
				result.UploadContentAsync ().Wait ();
				result.GetResponseAsync ().Wait ();
				return result;
			});
		}

		static void AddHeaders (WebHeaderCollection headers, IDictionary<string, string> add)
		{
			if (add == null)
				return;
			foreach (var e in add) {
				headers [e.Key] = e.Value;
			}
		}
	}
}

