// TODO split this file up, once we have stuff working pretty well ...

using System;
using System.IO;
using System.Net;
using System.Xml;
using System.Web;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Requestoring;

namespace EasyOData {

	namespace Filters {

		namespace Extensions {
			public static class FilterExtensions {
				public static Filters.Filter _Equals(this string propertyName, object value) {
					return new Filters.EqualsFilter(propertyName, value);
				}
			}
		}

		public class FilterList : List<Filter> {
		}

		public class Filter {
			public Filter() {}
			public Filter(string propertyName, object value) {
				PropertyName = propertyName;
				Value        = value;
			}

			public virtual string PropertyName { get; set; }
			public virtual object Value        { get; set; }

			public virtual string ValueString {
				get {
					if (Value is string)
						return "'" + Value + "'";
					else
						return Value.ToString();
				}
			}
		}

		public class SimpleOperatorFilter : Filter {
			public SimpleOperatorFilter(string propertyName, object value) : base(propertyName, value) {}

			public virtual string Operator { get { throw new Exception("You must override Operator in your SimpleOperatorFilter"); } }

			public override string ToString() {
				return string.Format("{0} {1} {2}", PropertyName, Operator, ValueString);
			}
		}

		public class EqualsFilter : SimpleOperatorFilter {
			public EqualsFilter(string propertyName, object value) : base(propertyName, value) {}
			public override string Operator { get { return "eq"; } }
		}

		public class NotEqualsFilter : SimpleOperatorFilter {
			public NotEqualsFilter(string propertyName, object value) : base(propertyName, value) {}
			public override string Operator { get { return "ne"; } }
		}
	}

	public static class XmlParsing {

		public static IEnumerable<XmlNode> GetElementsByTagName(this XmlNode node, string tagName) {
			var nodes = new List<XmlNode>();
			foreach (XmlNode child in node.ChildNodes)
				if (child.Name == tagName)
					nodes.Add(child);
			return nodes;
		}

		public static string Attr(this XmlNode node, string attributeName) {
			if (node.Attributes[attributeName] != null)
				return node.Attributes[attributeName].Value;
			else
				return null;
		}

		public static Property ToProperty(this XmlNode node) {
			return new Property {
				Name       = node.Attr("Name"),
				TypeName   = node.Attr("Type"),
				IsNullable = bool.Parse(node.Attr("Nullable"))
			};
		}

		public static EntityType ToEntityType(this XmlNode node, Metadata metadata) {
			var type = new EntityType();

			// Name
			type.Name = node.Attr("Name");

			// Properties
			foreach (XmlNode propertyNode in node.GetElementsByTagName("Property"))
				type.Properties.Add(propertyNode.ToProperty());

			// Keys
			foreach (XmlNode keyNode in node.GetElementsByTagName("Key"))
				foreach (XmlNode propertyRef in keyNode.GetElementsByTagName("PropertyRef"))
					type.Properties[propertyRef.Attr("Name")].IsKey = true;

			// Associations

			return type;
		}

		public static Collection ToCollection(this XmlNode node, Service service) {
			return new Collection {
				Name    = node["atom:title"].InnerText,
				Href    = node.Attr("href"),
				Service = service
			};
		}

	}

	public abstract class QueryOption {

		// 5
		// Name
		public virtual object Value { get; set; }

		// $top
		// $select
		public virtual string Key {
			get { return "$" + GetType().Name.Replace("QueryOption","").ToLower(); }
		}
		
		public QueryOption() {}
		public QueryOption(object value) {
			Value = value;
		}

		public virtual string AddQueryString(string path, string queryKey, string queryValue) {
			path += path.Contains("?") ? "&" : "?";
			return string.Format("{0}{1}={2}", path, queryKey, HttpUtility.UrlEncode(queryValue).Replace("+", "%20"));
		}

		public virtual string AddToPath(string path) {
			return AddQueryString(path, Key, Value.ToString());
		}
	}

	public class TopQueryOption : QueryOption {
		public TopQueryOption(object v) : base(v) {}

		public new int Value {
			get { return (int) base.Value; }
			set { base.Value = value;      }
		}
	}

	public class SkipQueryOption : QueryOption {
		public SkipQueryOption(object v) : base(v) {}

		public new int Value {
			get { return (int) base.Value; }
			set { base.Value = value;      }
		}
	}

	public class SelectQueryOption : QueryOption {
		public SelectQueryOption(object v) : base(v) {}

		public new string[] Value {
			get { return base.Value as string[]; }
			set { base.Value = value;            }
		}

		public override string AddToPath(string path) {
			return AddQueryString(path, Key, string.Join(",", Value).Replace(" ",""));
		}
	}

	public class ExpandQueryOption : QueryOption {
		public ExpandQueryOption(object v) : base(v) {}

		public new string[] Value {
			get { return base.Value as string[]; }
			set { base.Value = value;            }
		}

		public override string AddToPath(string path) {
			return AddQueryString(path, Key, string.Join(",", Value).Replace(" ",""));
		}
	}

	public class OrderByQueryOption : QueryOption {
		public OrderByQueryOption(object v) : base(v) {}

		public new string[] Value {
			get { return base.Value as string[]; }
			set { base.Value = value;            }
		}

		public override string AddToPath(string path) {
			return AddQueryString(path, Key, string.Join(",", Value));
		}
	}

	public class InlineCountQueryOption : QueryOption {
		public InlineCountQueryOption(bool allPages) {
			if (allPages)
				Value = "allpages";
			else
				Value = "none";
		}
	}

	// can specify a raw filter string 
	// or track abunchof Filters.Filter objects and 
	// use then to generate the path
	public class FilterQueryOption : QueryOption {
		public FilterQueryOption() : base() {
			Filters = new Filters.FilterList();
		}

		public new string Value {
			get { return base.Value as string; }
			set { base.Value = value;          }
		}

		public override string AddToPath(string path) {
			// raw Filter string specified
			if (Value != null)
				return AddQueryString(path, Key, Value);
			else
				return AddQueryString(path, Key, ValueForFilters);
		}

		public string ValueForFilters {
			// right now, we only support and ... we need to spec and/or queries to fix this!
			get { return string.Join(" and ", Filters.Select(f => f.ToString()).ToArray()); }
		}

		public FilterQueryOption(string rawFilterString) : this() {
			Value = rawFilterString;
		}

		public void Add(Filters.Filter filter) {
			Filters.Add(filter);
		}

		public Filters.FilterList Filters { get; set; }
	}

	public class Query : List<QueryOption> {
		public Collection Collection { get; set; }

		public Query() {}
		public Query(Collection collection) {
			Collection = collection;
		}

		public FilterQueryOption FilterQueryOption {
			get {
				var option = this.FirstOrDefault(o => o is FilterQueryOption) as FilterQueryOption;
				if (option == null) {
					option = new FilterQueryOption();
					this.Add(option);
				}
				return option;
			}
		}

		public void Add(Filters.Filter filter) {
			FilterQueryOption.Add(filter);
		}

		public string Path {
			get {
				//Console.WriteLine("Getting path for {0} which has {1} options", Collection.Href, this.Count);

				// start with the collection's Href
				var path = Collection.Href;

				// then let each QueryOption modify the path as necessary
				foreach (QueryOption option in this)
					path = option.AddToPath(path);

				// return the finished path
				return path;
			}
		}
	}

	public class Collection {
		public Service Service { get; set; }
		public string Name     { get; set; }
		public string Href     { get; set; }

		Query _query;
		Query Query {
			get {
				if (_query == null)
					_query = new Query(this);
				return _query;
			}
		}

		public Collection Top(int number) {
			Query.Add(new TopQueryOption(number));
			return this;
		}

		public Collection Skip(int number) {
			Query.Add(new SkipQueryOption(number));
			return this;
		}

		public Collection Select(params string[] propertyNames) {
			Query.Add(new SelectQueryOption(propertyNames));
			return this;
		}

		public Collection OrderBy(params string[] propertyNamesWithAscOrDesc) {
			Query.Add(new OrderByQueryOption(propertyNamesWithAscOrDesc));
			return this;
		}

		public Collection Expand(params string[] propertyOrAssociatinNames) {
			Query.Add(new ExpandQueryOption(propertyOrAssociatinNames));
			return this;
		}

		public Collection InlineCount() {
			Query.Add(new InlineCountQueryOption(true));
			return this;
		}

		public Collection NoInlineCount() {
			Query.Add(new InlineCountQueryOption(false));
			return this;
		}

		public Collection Filter(string rawFilterString) {
			Query.Add(new FilterQueryOption(rawFilterString));
			return this;
		}

		public Collection Where(Filters.Filter filter) {
			Query.Add(filter);
			return this;
		}

		public string ToPath() {
			var path = Query.Path;
			_query.Clear();
			return path;
		}
	}

	public class Property {
		public string Name { get; set; }
		public string TypeName { get; set; } // TODO this should eventually just get { EdmType.Name }
		public bool IsNullable { get; set; }
		public bool IsKey { get; set; }

		public string Type { get { return TypeName; } } // this should be the entity type, eventually
	}

	public class PropertyList : List<Property> {
		public PropertyList() {}
		public PropertyList(IEnumerable<Property> properties) {
			AddRange(properties);
		}

		public Property this[string name] {
			get { return this.FirstOrDefault(property => property.Name == name); }
		}
	}

	public class EntityType {
		public EntityType() {
			Properties = new PropertyList();
		}

		public string Name { get; set; }
		public PropertyList Properties { get; set; }

		public List<string> PropertyNames { get { return Properties.Select(property => property.Name).ToList(); } }

		public PropertyList Keys { get { return new PropertyList(Properties.Where(p => p.IsKey)); } }
	}

	public class EntityTypeList : List<EntityType> {
		public EntityType this[string name] {
			get { return this.FirstOrDefault(type => type.Name == name); }
		}
	}

	public class Metadata {

		public Metadata(){}
		public Metadata(Service service) {
			Service = service;
		}

		public Service Service { get; set; }

		public List<string> EntityTypeNames { get { return EntityTypes.Select(t => t.Name).ToList(); } }

		public EntityTypeList EntityTypes {
			get {
				var types = new EntityTypeList();
				foreach (XmlNode node in Service.GetXml("$metadata").GetElementsByTagName("EntityType"))
					types.Add(node.ToEntityType(this));
				return types;
			}
		}

		// public List<Property> Properties {
		// 	
		// }
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
					collections.Add(node.ToCollection(this));
				return collections;
			}
		}

		public Metadata Metadata {
			get { return new Metadata(this); }
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

		public XmlDocument GetXml(string path) {
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
