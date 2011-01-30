// TODO split this file up, once we have stuff working pretty well ...

using System;
using System.IO;
using System.Net;
using System.Xml;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Requestoring;

namespace EasyOData {

	public class Collection {
		public Service Service { get; set; }
		public string Name     { get; set; }
		public string Href     { get; set; }
	}

	/// <summary>Represents an OData Service</summary>
	public class Service {

		public string Root { get; set; }

		public Service() {}
		public Service(string root) {
			Root = root;
		}

		public List<Collection> Collections {
			get {
				var collections = new List<Collection>();
				
				foreach (XmlNode node in GetXml("/").GetElementsByTagName("collection"))
					collections.Add(new Collection {
						Name = node["atom:title"].InnerText,
						Href = node.Attributes["href"].Value
					});

				return collections;
			}
		}

		public List<string> CollectionNames {
			get { return Collections.Select(c => c.Name).ToList(); }
		}

		#region HTTP Requesting
		Requestor _requestor = new Requestor();

		string CombineUrl(string part1, string part2) {
			return part1.TrimEnd('/') + "/" + part2.TrimStart('/');
		}

		IResponse Get(string path) {
			return _requestor.Get(CombineUrl(Root, path));
		}

		XmlDocument GetXml(string path) {
			return GetXmlDocumentForString(Get(path).Body);
		}
		#endregion

		#region XML Parsing
		class UriSafeXmlResolver : XmlResolver {
			public override Uri ResolveUri (Uri baseUri, string relativeUri){ return baseUri; }
			public override object GetEntity (Uri absoluteUri, string role, Type type){ return null; }
			public override ICredentials Credentials { set {} }
		}

		static XmlDocument GetXmlDocumentForString(string xml) {
			var doc            = new XmlDocument();
			var reader         = new XmlTextReader(new StringReader(xml));
			reader.XmlResolver = new UriSafeXmlResolver();
			doc.Load(reader);
			return doc;
		}
		#endregion
	}
}
