using System;
using System.IO;
using System.Net;
using System.Xml;
using System.Web;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Requestoring;
using FluentXml;
using EasyOData.Conversions.Extensions; // TODO reverse, so you use EasyOData.Extensions.* ?

// So ... somehow I never split this into multiple files?  Crazy!  TODO - Organize this into multiple files ...

#if NET40
	using System.Dynamic;
#endif

namespace EasyOData {

	namespace Conversions.Extensions {

		public delegate bool TryParseDelegate<T>(string s, out T result);

		public static class CastingExtensions {

			public static int      TryParseInt(     this string value){ return value.TryParse<int>(     int.TryParse); }
			public static bool     TryParseBool(    this string value){ return value.TryParse<bool>(    bool.TryParse); }
			public static decimal  TryParseDecimal( this string value){ return value.TryParse<decimal>( decimal.TryParse); }
			public static double   TryParseDouble(  this string value){ return value.TryParse<double>(  double.TryParse); }
			public static Int64    TryParseInt64(   this string value){ return value.TryParse<Int64>(   Int64.TryParse); }
			public static DateTime TryParseDateTime(this string value){ return value.TryParse<DateTime>(DateTime.TryParse); }

			public static T TryParse<T>(this string value) {
				if (typeof(T) == typeof(int))      return (T) (object) value.TryParseInt();
				if (typeof(T) == typeof(bool))     return (T) (object) value.TryParseBool();
				if (typeof(T) == typeof(decimal))  return (T) (object) value.TryParseDecimal();
				if (typeof(T) == typeof(double))   return (T) (object) value.TryParseDouble();
				if (typeof(T) == typeof(Int64))    return (T) (object) value.TryParseInt64();
				if (typeof(T) == typeof(DateTime)) return (T) (object) value.TryParseDateTime();
				else
					throw new Exception(string.Format("Don't know how to TryParse T:{0}", typeof(T)));
			}

			public static T TryParse<T>(this string value, TryParseDelegate<T> parseDelegate) {
				T result;
				parseDelegate(value, out result);
				return result;
			}

			// If we have a string that we're casting *from*, check for NullOrEmpty
			public static T As<T>(this string o) {
				if (string.IsNullOrEmpty(o))
					return default(T);
				else
					return ((object) o).As<T>();
			}

			public static T As<T>(this object o) {
				if (o == null) return default(T);
				if (typeof(T).IsValueType)
					return o.ToString().TryParse<T>();
				else {
					return (T) o;
				}
			}
		}
	}

	public static class Util {

		// Encode using HttpUtility.UrlEncode, also encoding spaces to %20 and single quotes to %27
		public static string UrlEncode(this string s) {
			return HttpUtility.UrlEncode(s).Replace("+", "%20").Replace("'", "%27");
		}

		public static IDictionary<string,object> ToDictionary(this object anonymousType) {
			var dict = new Dictionary<string,object>();
			foreach (var property in anonymousType.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
				if (property.CanRead)
					dict.Add(property.Name, property.GetValue(anonymousType, null));
			return dict;
		}
	}

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
				get { return Query.ToValueString(Value); }
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

		public static Property ToProperty(this XmlNode node) {
			return new Property {
				Name       = node.Attr("Name"),
				TypeName   = node.Attr("Type"),
				IsNullable = (node.Attr("Nullable") == null) ? true : bool.Parse(node.Attr("Nullable"))
			};
		}

		public static string GetNextUrl(this XmlDocument doc) {
			foreach (XmlNode link in doc.Nodes("link"))
				if (link.Attr("rel") == "next")
					return link.Attr("href");
			return null;
		}

		public static List<Entity> ToEntities(this XmlDocument doc, Query query) {
			var entities = new List<Entity>();
			foreach (XmlNode node in doc.Nodes("entry"))
				entities.Add(node.ToEntity(query));
			return entities;
		}

		public static Entity ToEntity(this XmlNode node, Query query) {
			return node.ToEntity(query.Collection);
		}

		public static Entity ToEntity(this XmlNode node, Collection collection) {
			var entity     = new Entity { Xml = node.OuterXml };
			var metadata   = collection.Service.Metadata;

			// EntityType.  figure out what type of entity this is using the <category term="Full.Namespace.To.Class" />
			var fullName      = node.Nodes("category")[0].Attr("term");
			entity.EntityType = metadata.EntityTypes[fullName];
			if (entity.EntityType == null)
				throw new Exception(string.Format("Couldn't find EntityType by name: {0}", fullName));

			// Properties.  clone them from the EntityType, then fill in the values from the XML
			entity.Properties = new PropertyList();
			foreach (var property in entity.EntityType.Properties)
				entity.Properties.Add(property.Clone());

			foreach (XmlNode propertyNode in node.Nodes("m:properties")[0].ChildNodes) {
				var propertyName  = propertyNode.Name.Replace("d:", "");
				var propertyValue = propertyNode.InnerText;
				if (entity.Properties[propertyName] == null)
					throw new Exception(string.Format("EntityType {0} doesn't have property {1}", entity.EntityType.Name, propertyName));
				entity.Properties[propertyName].Text = propertyValue;
			}

			return entity;
		}

		public static EntityType ToEntityType(this XmlNode node, Metadata metadata) {
			var type = new EntityType { Namespace = metadata.Namespace, Service = metadata.Service };

			// Name
			type.Name         = node.Attr("Name");
			type.BaseTypeName = node.Attr("BaseType");

			// Properties
			foreach (XmlNode propertyNode in node.Nodes("Property"))
				type.CoreProperties.Add(propertyNode.ToProperty());

			// Keys
			foreach (XmlNode keyNode in node.Nodes("Key"))
				foreach (XmlNode propertyRef in keyNode.Nodes("PropertyRef"))
					type.CoreProperties[propertyRef.Attr("Name")].IsKey = true;

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
			return string.Format("{0}{1}={2}", path, queryKey, queryValue.UrlEncode());
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

	public class Query : List<Entity>, IList<Entity>, IEnumerable<Entity> {
		public Collection        Collection   { get; set; }
		public List<QueryOption> QueryOptions { get; set; }
		public int PagesToReturn { get; set; }
		public Service Service { get { return Collection.Service; } }

		public Query() {
			QueryOptions = new List<QueryOption>();
		}
		public Query(Collection collection) : this() {
			Collection = collection;
		}

		bool _executed = false;

		// Used to explicitly fire off the query.
		// All kicker methods call this.
		public Query Execute() {
			if (_executed == true) return this;

			_executed = true;
			var path  = ToPath();
			var pages = 0;

			while (path != null) {
				if (PagesToReturn > 0 && pages >= PagesToReturn) break;
				pages++;

				var xml = Service.GetXml(path);
				this.AddRange(xml.ToEntities(this));
				path = xml.GetNextUrl();
			}

			return this;
		}

		#region Kicker methods
		public new int Count {
			get { Execute(); return base.Count; }
		}

		public new IEnumerator<Entity> GetEnumerator() {
			Execute(); return base.GetEnumerator();
		}

		public Entity this[int index] {
			get { Execute(); return base[index]; }
		}

		public bool Contains(Entity entity) {
			Execute(); return base.Contains(entity);
		}

		public Entity First {
			get { return this.First(); }
		}

		public Entity Last {
			get { return this.Last(); }
		}
		#endregion

		public FilterQueryOption FilterQueryOption {
			get {
				var option = QueryOptions.FirstOrDefault(o => o is FilterQueryOption) as FilterQueryOption;
				if (option == null) {
					option = new FilterQueryOption();
					QueryOptions.Add(option);
				}
				return option;
			}
		}

		public Query Add(QueryOption option) {
			QueryOptions.Add(option);
			return this;
		}

		public Query Add(Filters.Filter filter) {
			FilterQueryOption.Add(filter);
			return this;
		}

		public string Path {
			get {
				// start with the collection's Href
				var path = Collection.Href;

				// then let each QueryOption modify the path as necessary
				foreach (QueryOption option in QueryOptions)
					path = option.AddToPath(path);

				// return the finished path
				return path;
			}
		}

		#region Query Building Methods
		public Query Pages(int number) {
			PagesToReturn = number;
			return this;
		}
		public Query Top(int number) {
			return Add(new TopQueryOption(number));
		}
		public Query Skip(int number) {
			return Add(new SkipQueryOption(number));
		}
		public Query Select(params string[] propertyNames) {
			return Add(new SelectQueryOption(propertyNames));
		}
		public Query OrderBy(params string[] propertyNamesWithAscOrDesc) {
			return Add(new OrderByQueryOption(propertyNamesWithAscOrDesc));
		}
		public Query Expand(params string[] propertyOrAssociatinNames) {
			return Add(new ExpandQueryOption(propertyOrAssociatinNames));
		}
		public Query InlineCount() {
			return Add(new InlineCountQueryOption(true));
		}
		public Query NoInlineCount() {
			return Add(new InlineCountQueryOption(false));
		}
		public Query Filter(string rawFilterString) {
			return And(rawFilterString);
		}
		public Query Where(string rawFilterString) {
			return And(rawFilterString);
		}
		public Query And(string rawFilterString) {
			return And(new Filters.RawFilter(rawFilterString));
		}
		public Query Or(string rawFilterString) {
			return Or(new Filters.RawFilter(rawFilterString));
		}
		public Query Where(Filters.Filter filter) {
			return Add(filter);
		}
		public Query And(Filters.Filter filter) {
			return Add(filter);
		}
		public Query Or(Filters.Filter filter) {
			filter.Or = true;
			return Add(filter);
		}

		// calling ToPath() clears out the query options
		public string ToPath() {
			var path = Path;
			QueryOptions.Clear();
			return path;
		}
		public string ToUrl() {
			return Collection.Service.GetUrl(ToPath());
		}
		#endregion

		public static string ToValueString(object o) {
			if (o is string)
				return string.Format("'{0}'", o);
			else if (o is Guid)
				return string.Format("guid'{0}'", o);
			else
				return o.ToString();
		}

		public static string ToEncodedValueString(object o) {
			if (o is string)
				return string.Format("'{0}'", (o as string).UrlEncode());
			else if (o is Guid)
				return string.Format("guid'{0}'", o);
			else
				return o.ToString();
		}
	}

	public class CollectionList : List<Collection> {
		public Collection this[string name] {
			get { return this.FirstOrDefault(collection => collection.Name == name); }
		}
	}

	public class Collection {
		public Service Service { get; set; }
		public string Name     { get; set; }
		public string Href     { get; set; }

		Query Query { get { return new Query(this); } }

		public Entity GetByKey(string key) {
			var path = string.Format("{0}({1})", Href, key);
			var xml  = Service.GetXml(path);
			if (xml == null)
				return null;
			else
				return xml.Nodes("entry")[0].ToEntity(this);
		}

		public Entity Get(string key) {
			return GetByKey(Query.ToEncodedValueString(key));
		}

		public Entity Get(IDictionary<string,object> keys) {
			return GetByKey(string.Join(",", keys.Select(item => string.Format("{0}={1}", item.Key, Query.ToEncodedValueString(item.Value))).ToArray()));
		}

		public Entity Get(object key) {
			if (key.GetType().Name.Contains("AnonType"))
				return Get(key.ToDictionary());
			else
				return GetByKey(Query.ToEncodedValueString(key));
		}

		public Entity First { get { return Query.First; } }
		public Entity Last  { get { return Query.Last;  } }
		public int    Count { get { return Query.Count; } }
		public Query  All   { get { return Query;       } }

		// We delegate all Querying calls to Query allowing us to say collection.Top() instead of collection.Query.Top();
		public Query Pages(int number)              { return Query.Pages(number);           }
		public Query Top(int number)                { return Query.Top(number);             }
		public Query Skip(int number)               { return Query.Skip(number);            }
		public Query Select(params string[] names)  { return Query.Select(names);           }
		public Query OrderBy(params string[] names) { return Query.OrderBy(names);          }
		public Query Expand(params string[] names)  { return Query.Expand(names);           }
		public Query InlineCount()                  { return Query.InlineCount();           }
		public Query NoInlineCount()                { return Query.NoInlineCount();         }
		public Query Filter(string rawFilterString) { return Query.Filter(rawFilterString); }
		public Query Where(string rawFilterString)  { return Query.Where(rawFilterString);  }
		public Query And(string rawFilterString)    { return Query.And(rawFilterString);    }
		public Query Or(string rawFilterString)     { return Query.Or(rawFilterString);     }
		public Query Where(Filters.Filter filter)   { return Query.Where(filter);           }
		public Query And(Filters.Filter filter)     { return Query.And(filter);             }
		public Query Or(Filters.Filter filter)      { return Query.Or(filter);              }
		public string ToPath()                      { return Query.ToPath();                }
		public string ToUrl()                       { return Query.ToUrl();                 }
	}

	public class Property {
		public string Name { get; set; }
		public string TypeName { get; set; } // TODO this should eventually just get { EdmType.Name }
		public bool   IsNullable { get; set; }
		public bool   IsKey { get; set; }
		public string Text { get; set; }
		public string Type { get { return TypeName; } } // this should be the entity type, eventually

		// Returns Text, casted to the appropriate type, per the Edm.* TypeName
		public object Value {
			get {
				switch (TypeName) {
					case "Edm.String":   return Text.As<string>();
					case "Edm.Boolean":  return Text.As<bool>();
					case "Edm.DateTime": return Text.As<DateTime>();
					case "Edm.Int32":    return Text.As<int>();
					case "Edm.Int64":    return Text.As<Int64>();
					case "Edm.Double":   return Text.As<double>();
					case "Edm.Decimal":  return Text.As<decimal>();
					default: 
						Console.WriteLine("Unknown type: {0}", TypeName);
						return Text;
				}
			}
			set { Text = (value == null) ? null : value.ToString(); }
		}

		public Property Clone() {
			return new Property {
				Name       = this.Name,
				TypeName   = this.TypeName,
				IsNullable = this.IsNullable,
				IsKey      = this.IsKey,
				Value      = this.Value
			};
		}

		public override string ToString() {
			return string.Format("{0}: {1}", Name, Value);
		}
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

	public class Entity
#if NET40
		: IDynamicMetaObjectProvider 
#endif
{

#if NET40
		public DynamicMetaObject GetMetaObject(System.Linq.Expressions.Expression e){ return new MetaObject(e, this); }

		public bool TryGetMember(GetMemberBinder binder, out object result) {
			var property = Properties[binder.Name];
			if (property == null) {
				result = null;
				return false;
			} else {
				result = property.Value;
				return true;
			}
		}
#endif

		public Entity() {
			Properties = new PropertyList();
		}

		public virtual string Xml { get; set; }
		public virtual EntityType EntityType { get; set; }
		public virtual PropertyList Properties { get; set; }

		public virtual List<string> PropertyNames {
			get { return Properties.Select(p => p.Name).ToList(); }
		}

		// Shortcut to get the value of a property
		public virtual object this[string propertyName] {
			get {
				var property = Properties[propertyName];
				if (property == null)
					return null;
				else
					return property.Value;
			}
		}
	}

	public class EntityType {
		public EntityType() {
			CoreProperties = new PropertyList();
		}

		public string Name         { get; set; }
		public string BaseTypeName { get; set; }
		public string Namespace    { get; set; }
		public Service Service     { get; set; }

		public string FullName {
			get { return Namespace + "." + Name; }
		}

		public EntityType BaseType {
			get {
				return (BaseTypeName == null) ? null : Service.EntityTypes[BaseTypeName];
			}
		}

		public PropertyList Keys { get { return new PropertyList(Properties.Where(p => p.IsKey)); } }

		public PropertyList CoreProperties { get; set; }
		public PropertyList BaseProperties {
			get {
				return (BaseType == null) ? null : BaseType.Properties;
			}
		}
		public PropertyList Properties {
			get {
				var properties = new PropertyList();
				if (BaseProperties != null) properties.AddRange(BaseProperties);
				if (CoreProperties != null) properties.AddRange(CoreProperties);
				return properties;
			}
		}

		public List<string> CorePropertyNames { get { return CoreProperties.Select(property => property.Name).OrderBy(name => name).ToList(); } }
		public List<string> BasePropertyNames { get { return BaseProperties.Select(property => property.Name).OrderBy(name => name).ToList(); } }
		public List<string> PropertyNames     { get { return Properties.Select(property => property.Name).OrderBy(name => name).ToList();     } }

		public override bool Equals(object o) {
			if (o == null || o.GetType() != GetType()) return false;
			
			return (o as EntityType).FullName == FullName;
		}
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
			Namespace = doc.Nodes("Schema")[0].Attr("Namespace");

			// get entity types
			var types = new EntityTypeList();
			foreach (XmlNode node in doc.Nodes("EntityType"))
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
	public class Service 
#if NET40
	: IDynamicMetaObjectProvider
#endif
{

#if NET40
		public DynamicMetaObject GetMetaObject(System.Linq.Expressions.Expression e){ return new MetaObject(e, this); }

		public bool TryGetMember(GetMemberBinder binder, out object result) {
			result = Collections[binder.Name];
			return (result != null);
		}
#endif

		public string Root { get; set; }

		public Service() {}
		public Service(string root) {
			Root = root;
		}

		public Collection this[string collectionName] {
			get { return Collections[collectionName]; }
		}

		CollectionList _collections;
		public CollectionList Collections {
			get {
				if (_collections == null) {
					_collections = new CollectionList();
					foreach (XmlNode node in GetXml("/").Nodes("collection"))
						_collections.Add(node.ToCollection(this));
				}
				return _collections;
			}
		}

		public EntityTypeList EntityTypes {
			get { return Metadata.EntityTypes; }
		}

		Metadata _metadata;
		public Metadata Metadata {
			get {
				if (_metadata == null)
					_metadata = new Metadata(this);
				return _metadata;
			}
		}

		public List<string> CollectionNames {
			get { return Collections.Select(c => c.Name).OrderBy(name => name).ToList(); }
		}

		public string GetUrl(string relativePath) {
			return (IsAbsoluteUrl(relativePath)) ? relativePath : CombineUrl(Root, relativePath);
		}

		bool IsAbsoluteUrl(string path) {
			return Regex.IsMatch(path, @"^\w+://"); // if it starts with whatever://, then it's absolute.
		}

		#region HTTP Requesting
		Requestor _requestor = new Requestor();

		string CombineUrl(string part1, string part2) {
			return part1.TrimEnd('/') + "/" + part2.TrimStart('/');
		}

		public IResponse Get(string path) {
			return _requestor.Get(GetUrl(path));
		}

		public XmlDocument GetXml(string path) {
			var response = Get(path);
			if (response.Status == 404)
				return null;
			else
				return GetXmlDocumentForString(response.Body);
		}
		#endregion

		#region XML Parsing
		class UriSafeXmlResolver : XmlResolver {
			public override Uri ResolveUri (Uri baseUri, string relativeUri){ return baseUri; }
			public override object GetEntity (Uri absoluteUri, string role, Type type){ return null; }
			public override ICredentials Credentials { set {} }
		}

		public static XmlDocument GetXmlDocumentForString(string xml) {
			var doc            = new XmlDocument();
			var reader         = new XmlTextReader(new StringReader(xml));
			reader.XmlResolver = new UriSafeXmlResolver();
			doc.Load(reader);
			return doc;
		}
		#endregion
	}
}
