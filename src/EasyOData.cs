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

				public static Filters.Filter _NotEqual(this string propertyName, object value) {
					return new Filters.NotEqualsFilter(propertyName, value);
				}

				public static Filters.Filter _StartsWith(this string propertyName, object value) {
					return new Filters.StartsWithFilter(propertyName, value);
				}

				public static Filters.Filter _EndsWith(this string propertyName, object value) {
					return new Filters.EndsWithFilter(propertyName, value);
				}

				public static Filters.Filter _Contains(this string propertyName, object value) {
					return new Filters.ContainsFilter(propertyName, value);
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

			bool _and = true;

			public bool And {
				get { return _and;  }
				set { _and = value; }
			}

			public bool Or {
				get { return ! And;  }
				set { And = ! value; }
			}

			public string AndOrString {
				get { return And ? "and" : "or"; }
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

		public class RawFilter : Filter {
			public RawFilter(string raw) {
				Raw = raw;
			}

			public string Raw { get; set; }

			public override string ToString() {
				return Raw;
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

		public class StartsWithFilter : Filter {
			public StartsWithFilter(string propertyName, object value) : base(propertyName, value) {}
			public override string ToString() {
				return string.Format("startswith({0}, {1}) eq true", PropertyName, ValueString);
			}
		}

		public class EndsWithFilter : Filter {
			public EndsWithFilter(string propertyName, object value) : base(propertyName, value) {}
			public override string ToString() {
				return string.Format("endswith({0}, {1}) eq true", PropertyName, ValueString);
			}
		}

		public class ContainsFilter : Filter {
			public ContainsFilter(string propertyName, object value) : base(propertyName, value) {}
			public override string ToString() {
				return string.Format("substringof({0}, {1}) eq true", ValueString, PropertyName);
			}
		}
	}

	public static class XmlParsing {

		public static List<XmlNode> GetElementsByTagName(this XmlNode node, string tagName) {
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

		public static List<Entity> ToEntities(this XmlDocument doc, Collection collection) {
			var entities = new List<Entity>();
			foreach (XmlNode node in doc.GetElementsByTagName("entry"))
				entities.Add(node.ToEntity(collection));
			return entities;
		}

		public static Entity ToEntity(this XmlNode node, Collection collection) {
			var entity   = new Entity();
			var metadata = collection.Service.Metadata;

			// EntityType.  figure out what type of entity this is using the <category term="Full.Namespace.To.Class" />
			var fullName      = node.GetElementsByTagName("category")[0].Attr("term");
			entity.EntityType = metadata.EntityTypes[fullName];
			if (entity.EntityType == null)
				throw new Exception(string.Format("Couldn't find EntityType by name: {0}", fullName));

			// Properties.  clone them from the EntityType, then fill in the values from the XML
			entity.Properties = new PropertyList(entity.EntityType.Properties);
			foreach (XmlNode propertyNode in node.GetElementsByTagName("m:properties")[0].ChildNodes) {
				var propertyName  = propertyNode.Name.Replace("d:", "");
				var propertyValue = propertyNode.InnerText;
				if (entity.Properties[propertyName] == null)
					throw new Exception(string.Format("EntityType {0} doesn't have property {1}", entity.EntityType.Name, propertyName));
				entity.Properties[propertyName].Value = propertyValue;
			}

			return entity;
		}

		public static EntityType ToEntityType(this XmlNode node, Metadata metadata) {
			var type = new EntityType { Namespace = metadata.Namespace };

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
			// get { return string.Join(" and ", Filters.Select(f => f.ToString()).ToArray()); }

			get {
				var filterString = "";

				for (int i = 0; i < Filters.Count; i++) {
					var filter = Filters[i];
					if (i > 0)
						filterString += " " + filter.AndOrString + " ";
					filterString += filter.ToString();
				}

				return filterString;
			}
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

	public class CollectionList : List<Collection> {
		public Collection this[string name] {
			get { return this.FirstOrDefault(collection => collection.Name == name); }
		}
	}

	public class Collection : List<Entity> {
		public Service Service { get; set; }
		public string Name     { get; set; }
		public string Href     { get; set; }

		// Kicker methods all call this
		public void ExecuteQuery() {
			if (Query != null) {
				Console.WriteLine("Executing Query");
				this.AddRange(Service.GetXml(ToPath()).ToEntities(this));
				Console.WriteLine("DONE");
			}
		}

		#region Kicker Methods
		public new int Count {
			get {
				ExecuteQuery();
				return base.Count;
			}
		}

		// Can't seem to get this to work so, for now, you need to call Run() for Linq
		//public new List<Entity>.Enumerator GetEnumerator() {
		//	ExecuteQuery();
		//	return base.GetEnumerator();
		//}
		#endregion

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

		// Raw Filter Strings
		public Collection Filter(string rawFilterString) {
			return And(rawFilterString);
		}
		public Collection Where(string rawFilterString) {
			return And(rawFilterString);
		}
		public Collection And(string rawFilterString) {
			return And(new Filters.RawFilter(rawFilterString));
		}
		public Collection Or(string rawFilterString) {
			return Or(new Filters.RawFilter(rawFilterString));
		}

		public Collection Where(Filters.Filter filter) {
			Query.Add(filter);
			return this;
		}

		public Collection And(Filters.Filter filter) {
			Query.Add(filter);
			return this;
		}

		public Collection Or(Filters.Filter filter) {
			filter.Or = true;
			Query.Add(filter);
			return this;
		}

		public string ToPath() {
			var path = Query.Path;
			_query = null;
			return path;
		}

		public string ToUrl() {
			return Service.GetUrl(ToPath());
		}
	}

	public class Property {
		public string Name { get; set; }
		public string TypeName { get; set; } // TODO this should eventually just get { EdmType.Name }
		public bool IsNullable { get; set; }
		public bool IsKey { get; set; }
		public object Value { get; set; }

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

	public class Entity {
		public Entity() {
			Properties = new PropertyList();
		}

		public EntityType EntityType { get; set; }
		public PropertyList Properties { get; set; }
	}

	public class EntityType {
		public EntityType() {
			Properties = new PropertyList();
		}

		public string Name             { get; set; }
		public string Namespace        { get; set; }
		public PropertyList Properties { get; set; }

		public string FullName {
			get { return Namespace + "." + Name; }
		}

		public List<string> PropertyNames { get { return Properties.Select(property => property.Name).ToList(); } }

		public PropertyList Keys { get { return new PropertyList(Properties.Where(p => p.IsKey)); } }
	}

	public class EntityTypeList : List<EntityType> {
		public EntityType this[string name] {
			get {
				if (name.Contains("."))
					return ByFullName(name);
				else
					return ByClassName(name);
			}
		}
		public EntityType ByClassName(string className) {
			return this.FirstOrDefault(type => type.Name == className);
		}
		public EntityType ByFullName(string fullName) {
			return this.FirstOrDefault(type => type.FullName == fullName);
		}
	}

	public class Metadata {

		public Metadata(){}
		public Metadata(Service service) {
			Service = service;
		}

		public Service Service { get; set; }

		public List<string> EntityTypeNames { get { return EntityTypes.Select(t => t.Name).ToList(); } }

		// Kicker method for getting and parsing Metadata
		public void GetAndParseMetadata() {
			var doc = Service.GetXml("$metadata");

			// get namespace
			Namespace = doc.GetElementsByTagName("Schema")[0].Attr("Namespace");

			// get entity types
			var types = new EntityTypeList();
			foreach (XmlNode node in doc.GetElementsByTagName("EntityType"))
				types.Add(node.ToEntityType(this));
			EntityTypes = types;
		}

		string _namespace;
		public string Namespace {
			get {
				if (_namespace == null)
					GetAndParseMetadata();
				return _namespace;
			}
			set { _namespace = value; }
		}

		EntityTypeList _entityTypes;
		public EntityTypeList EntityTypes {
			get {
				if (_entityTypes == null)
					GetAndParseMetadata();
				return _entityTypes;
			}
			set { _entityTypes = value; }
		}
	}

	/// <summary>Represents an OData Service</summary>
	public class Service {

		public string Root { get; set; }

		public Service() {}
		public Service(string root) {
			Root = root;
		}

		public CollectionList Collections {
			get {
				var collections = new CollectionList();
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

		public string GetUrl(string relativePath) {
			return CombineUrl(Root, relativePath);
		}

		#region HTTP Requesting
		Requestor _requestor = new Requestor();

		string CombineUrl(string part1, string part2) {
			return part1.TrimEnd('/') + "/" + part2.TrimStart('/');
		}

		IResponse Get(string path) {
			return _requestor.Get(GetUrl(path));
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
