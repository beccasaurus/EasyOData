using System;
using System.Linq;
using System.Collections.Generic;
using Requestoring;
using EasyOData;
using EasyOData.Filters.Extensions; // gives us the _Equals() style extension methods
using NUnit.Framework;

namespace EasyOData.Specs {

	public static class SpecExtensions {
		public static string Encode(this string hasSpaces) {
			return hasSpaces.Replace(" ", "%20").Replace(",", "%2c").Replace("'", "%27");
		}
	}

	[TestFixture]
	public class NugetExampleSpec : Spec {

		string NuGetServiceRoot = "http://packages.nuget.org/v1/FeedService.svc/";
		Service service;

		[SetUp]
		public void Before() {
			base.Before();
			FakeResponse(NuGetServiceRoot,                             "NuGet", "root.xml");
			FakeResponse(NuGetServiceRoot + "$metadata",               "NuGet", "metadata.xml");
			FakeResponse(NuGetServiceRoot + "Packages?$top=1",         "NuGet", "Packages_top_1.xml");
			FakeResponse(NuGetServiceRoot + "Packages?$top=3",         "NuGet", "Packages_top_3.xml");
			FakeResponse(NuGetServiceRoot + "Packages?$top=3&$skip=2", "NuGet", "Packages_top_3_skip_2.xml");
			FakeResponse(NuGetServiceRoot + "Packages(Id='NUnit',Version='2.5.7.10213')", "NuGet", "Packages_NUnit.xml");
			service = new Service(NuGetServiceRoot);
		}

		[Test]
		public void can_get_via_keys_using_dictionary() {
			var package = service["Packages"].Get(new Dictionary<string,object>{ {"Id","NUnit"}, {"Version","2.5.7.10213"} });
			package["Id"].ShouldEqual("NUnit");
			package["Version"].ShouldEqual("2.5.7.10213");
			package["PackageSize"].ToString().ShouldEqual("763203");
			package["Summary"].ToString().ShouldContain("NUnit is a unit-testing framework");
		}

		[Test]
		public void can_get_via_keys_using_anonymous_object() {
			var package = service["Packages"].Get(new { Id ="NUnit", Version = "2.5.7.10213" });
			package["Id"].ShouldEqual("NUnit");
			package["Version"].ShouldEqual("2.5.7.10213");
			package["PackageSize"].ToString().ShouldEqual("763203");
			package["Summary"].ToString().ShouldContain("NUnit is a unit-testing framework");
		}

		[Test]
		public void can_get_top_1_package() {
			// Check the Path and Url
			service.Collections["Packages"].Top(1).ToPath().ShouldEqual("Packages?$top=1");
			service.Collections["Packages"].Top(1).ToUrl().ShouldEqual("http://packages.nuget.org/v1/FeedService.svc/Packages?$top=1");

			// i don't like that the Collection instance is what we get ...
			var packages = service.Collections["Packages"].Top(1);
			(packages is Query).ShouldBeTrue();

			// Count
			packages = service.Collections["Packages"].Top(1);
			packages.Count.ShouldEqual(1);

			// foreach
			var myEntities = new List<Entity>();
			packages = service.Collections["Packages"].Top(1);
			foreach (var entity in packages)
				myEntities.Add(entity);
			myEntities.Count.ShouldEqual(1);

			// make a new list with the packages
			packages = service.Collections["Packages"].Top(1);
			var entities = new List<Entity>(packages);
			entities.Count.ShouldEqual(1);

			// LINQ
			packages = service.Collections["Packages"].Top(1);

			var first = packages.First();
			first.EntityType.Name.ShouldEqual("PublishedPackage");

			first.Properties["Id"].Value.ShouldEqual("51Degrees.mobi");
			first.Properties["Id"].Type.ToString().ShouldEqual("Edm.String");
			first.Properties["Id"].IsNullable.ShouldBeFalse();

			first.Properties["Version"].Value.ShouldEqual("0.1.11.10");
			first.Properties["Title"].Value.ShouldEqual("51Degrees.mobi");

			first.Properties["Authors"].Value.ShouldEqual("James Rosewell,  Thomas Holmes");
			first.Properties["Authors"].IsNullable.ShouldBeTrue();

			first.Properties["PackageSize"].Type.ToString().ShouldEqual("Edm.Int64");
			first.Properties["PackageSize"].IsNullable.ShouldBeFalse();

			first.Properties["IsLatestVersion"].Type.ToString().ShouldEqual("Edm.Boolean");
			first.Properties["IsLatestVersion"].IsNullable.ShouldBeFalse();
		}

		[Test]
		public void can_get_top_3_packages() {
			var packages = service.Collections["Packages"].Top(3);

			packages[0]["Id"].ShouldEqual("51Degrees.mobi");
			packages[1]["Id"].ShouldEqual("Adam.JSGenerator");
			packages[2]["Id"].ShouldEqual("AE.Net.Mail");

			packages.Count.ShouldEqual(3);
		}

		[Test]
		public void can_get_top_3_packages_skipping_2() {
			var packages = service.Collections["Packages"].Top(3).Skip(2);

			packages[0]["Id"].ShouldEqual("AE.Net.Mail");
			packages[1]["Id"].ShouldEqual("Agatha-rrsl");
			packages[2]["Id"].ShouldEqual("Altairis.MailToolkit");

			packages.Count.ShouldEqual(3);
		}

		[Test][Ignore]
		public void can_get_package_by_key() {
		}

		[Test][Ignore]
		public void can_get_top_packages_with_name_starting_with_something() {
		}

		// Note, this isn't specific to NugetExampleSpec and can be moved ...
		//
		// TODO move this [Test] and split it into 1 [Test] per QueryOption
		//
		[Test]
		public void can_get_path_that_would_be_queried() {
			var comma = "%2c";
			var slash = "%2f";

			var collection = new Collection {
				Service = new Service(),
				Href    = "Dogs"
			};

			// No Filters
			collection.ToPath().ShouldEqual("Dogs");

			// Top
			collection.Top(1).ToPath().ShouldEqual("Dogs?$top=1");
			collection.Top(5).ToPath().ShouldEqual("Dogs?$top=5");

			// Skip
			collection.Skip(1).ToPath().ShouldEqual("Dogs?$skip=1");
			collection.Skip(5).ToPath().ShouldEqual("Dogs?$skip=5");

			// Top and Skip
			collection.Top(1).Skip(1).ToPath().ShouldEqual("Dogs?$top=1&$skip=1");
			collection.Skip(5).Top(3).ToPath().ShouldEqual("Dogs?$skip=5&$top=3");

			// Select
			collection.Select("*").ToPath().ShouldEqual("Dogs?$select=*");
			collection.Select("Name").ToPath().ShouldEqual("Dogs?$select=Name");
			collection.Select("Name", "Category").ToPath().ShouldEqual(string.Format("Dogs?$select=Name{0}Category", comma));
			collection.Select("Name", "Category,Foo").ToPath().ShouldEqual(string.Format("Dogs?$select=Name{0}Category{0}Foo", comma));
			collection.Top(3).Select("Name", "Category,  Foo").Skip(4).ToPath().
				ShouldEqual(string.Format("Dogs?$top=3&$select=Name{0}Category{0}Foo&$skip=4", comma));

			// OrderBy
			collection.OrderBy("*").ToPath().ShouldEqual("Dogs?$orderby=*");
			collection.OrderBy("Name").ToPath().ShouldEqual("Dogs?$orderby=Name");
			collection.OrderBy("Name", "Category").ToPath().ShouldEqual(string.Format("Dogs?$orderby=Name{0}Category", comma));
			collection.OrderBy("Name", "Category,Foo").ToPath().ShouldEqual(string.Format("Dogs?$orderby=Name{0}Category{0}Foo", comma));
			collection.Top(3).OrderBy("Name", "Category,Foo desc").Skip(4).ToPath().
				ShouldEqual(string.Format("Dogs?$top=3&$orderby=Name{0}Category{0}Foo%20desc&$skip=4", comma));
			collection.Select("Name").OrderBy("Name", "Category").Top(3).ToPath().
				ShouldEqual(string.Format("Dogs?$select=Name&$orderby=Name{0}Category&$top=3", comma));

			// Expand
			collection.Expand("*").ToPath().ShouldEqual("Dogs?$expand=*");
			collection.Expand("Name").ToPath().ShouldEqual("Dogs?$expand=Name");
			collection.Expand("Name", "Category").ToPath().ShouldEqual(string.Format("Dogs?$expand=Name{0}Category", comma));
			collection.Expand("Name", "Products/Suppliers").ToPath().ShouldEqual(string.Format("Dogs?$expand=Name{0}Products{1}Suppliers", comma, slash));
			collection.Top(4).Expand("Name", "Category,Foo").Skip(2).ToPath().
				ShouldEqual(string.Format("Dogs?$top=4&$expand=Name{0}Category{0}Foo&$skip=2", comma));

			// InlineCount
			collection.InlineCount().ToPath().ShouldEqual("Dogs?$inlinecount=allpages");
			collection.Top(1).InlineCount().ToPath().ShouldEqual("Dogs?$top=1&$inlinecount=allpages");
			collection.NoInlineCount().ToPath().ShouldEqual("Dogs?$inlinecount=none");
			collection.Top(3).NoInlineCount().Skip(4).ToPath().ShouldEqual("Dogs?$top=3&$inlinecount=none&$skip=4");

			// Format
			// 
			// Not adding support for $format because the typicaly OData WCF services don't actually seem 
			// to support this value, plus EasyOData will be working with atom+xml data as JSON isn't very 
			// well supported by WCF providers.
			//

			// Filter <--- probably won't be using a RAW Filter, but I want to support it!
			collection.Filter("Price gt 20").ToPath().ShouldEqual("Dogs?$filter=Price gt 20".Encode());
			collection.Top(4).Filter("Price le 200 and Price gt 3.5").Skip(2).ToPath().
				ShouldEqual("Dogs?$top=4&$filter=Price le 200 and Price gt 3.5&$skip=2".Encode());
			
			// custom filter methods ...

			// low-level ... part of the public API incase people want to dynamically make queries or make their own extension 
			// methods or for when we want to make a LINQ provider or something sexy like that ...
			collection.Where(new Filters.EqualsFilter("Name", "Bob")).ToPath().ShouldEqual("Dogs?$filter=Name eq 'Bob'".Encode());
			collection.Where(new Filters.EqualsFilter("Id", 15)).ToPath().ShouldEqual("Dogs?$filter=Id eq 15".Encode());

			collection.Where(new Filters.NotEqualsFilter("Name", "Bob")).ToPath().ShouldEqual("Dogs?$filter=Name ne 'Bob'".Encode());
			collection.Where(new Filters.NotEqualsFilter("Id", 15)).ToPath().ShouldEqual("Dogs?$filter=Id ne 15".Encode());

			// Equals (using anonymous object)
			//collection.Where(new { Name = "Bob" }).ToPath().ShouldEqual("Dogs?$filter=Name%20eq'Bob'");
			
			// Equals
			collection.Where("Name"._Equals("Bob")).ToPath().ShouldEqual("Dogs?$filter=Name eq 'Bob'".Encode());
			collection.Top(1).Where("Name"._Equals("Bob")).Skip(30).ToPath().ShouldEqual("Dogs?$top=1&$filter=Name eq 'Bob'&$skip=30".Encode());
			
			// NotEqual
			collection.Where("Name"._NotEqual("Bob")).ToPath().ShouldEqual("Dogs?$filter=Name ne 'Bob'".Encode());
			collection.Top(1).Where("Name"._NotEqual("Bob")).Skip(30).ToPath().ShouldEqual("Dogs?$top=1&$filter=Name ne 'Bob'&$skip=30".Encode());

			// Where().Where()
			collection.Where("Name"._Equals("Bob")).Where("Foo"._NotEqual(5)).ToPath().
				ShouldEqual("Dogs?$filter=Name eq 'Bob' and Foo ne 5".Encode());

			// Where().And()
			collection.Where("Name"._Equals("Bob")).And("Foo"._NotEqual(5)).ToPath().
				ShouldEqual("Dogs?$filter=Name eq 'Bob' and Foo ne 5".Encode());

			// Where().Or()
			collection.Where("Name"._Equals("Bob")).Or("Foo"._NotEqual(5)).ToPath().
				ShouldEqual("Dogs?$filter=Name eq 'Bob' or Foo ne 5".Encode());

			// Where("raw string").And(x = y)
			collection.Where("Id eq 5").And("Name"._NotEqual("Bob")).Or("Foo"._Equals(5)).ToPath().
				ShouldEqual("Dogs?$filter=Id eq 5 and Name ne 'Bob' or Foo eq 5".Encode());

			// Where(x = y).And("raw string")
			collection.Where("Name"._NotEqual("Bob")).And("Id eq 5").Or("Foo"._Equals(5)).ToPath().
				ShouldEqual("Dogs?$filter=Name ne 'Bob' and Id eq 5 or Foo eq 5".Encode());

			// Where(x = y).Or("raw string")
			collection.Where("Name"._NotEqual("Bob")).Or("Id eq 5").Or("Foo"._Equals(5)).And("more raw").ToPath().
				ShouldEqual("Dogs?$filter=Name ne 'Bob' or Id eq 5 or Foo eq 5 and more raw".Encode());

			// StartsWith
			collection.Where("Name"._StartsWith("Bob")).ToPath().ShouldEqual("Dogs?$filter=startswith(Name, 'Bob') eq true".Encode());

			// EndsWith
			collection.Where("Name"._EndsWith("Smith")).Or("Name"._StartsWith("Bob")).ToPath().
				ShouldEqual("Dogs?$filter=endswith(Name, 'Smith') eq true or startswith(Name, 'Bob') eq true".Encode());

			// Contains
			collection.Where("Name"._Contains("Bob")).ToPath().ShouldEqual("Dogs?$filter=substringof('Bob', Name) eq true".Encode());
		}

		[Test]
		public void can_get_collection_names() {
			service.CollectionNames.Count.ShouldEqual(2);
			service.CollectionNames.ShouldContain("Packages");
			service.CollectionNames.ShouldContain("Screenshots");
		}

		[Test]
		public void can_get_collections() {
			service.Collections.Count.ShouldEqual(2);

			service.Collections.First().Name.ShouldEqual("Packages");
			service.Collections.First().Href.ShouldEqual("Packages");
			service.Collections.First().Service.ShouldEqual(service);

			service.Collections.Last().Name.ShouldEqual("Screenshots");
			service.Collections.Last().Href.ShouldEqual("Screenshots");
			service.Collections.Last().Service.ShouldEqual(service);
		}

		[Test]
		public void can_get_metadata() {
			var metadata = service.Metadata;

			// <EntityType>
			metadata.EntityTypes.Count.ShouldEqual(2);

			metadata.EntityTypeNames.Count.ShouldEqual(2);
			metadata.EntityTypeNames.ShouldContain("PublishedPackage");
			metadata.EntityTypeNames.ShouldContain("PublishedScreenshot");

			var package = metadata.EntityTypes["PublishedPackage"];

			// can use full namespace
			metadata.EntityTypes["Gallery.Infrastructure.FeedModels.PublishedPackage"].ShouldEqual(package);

			package.Name.ShouldEqual("PublishedPackage");
			package.FullName.ShouldEqual("Gallery.Infrastructure.FeedModels.PublishedPackage");

			// // <Property>
			package.Properties.Count.ShouldEqual(32);

			new List<string> {
				"Id", "Version", "Title", "Authors", "PackageType", "Summary", "Description", "Copyright",
				"PackageHashAlgorithm", "PackageHash", "PackageSize", "Price", "RequireLicenseAcceptance", 
				"IsLatestVersion", "VersionRating", "VersionRatingsCount", "VersionDownloadCount", "Created",
				"LastUpdated", "Published", "ExternalPackageUrl", "ProjectUrl", "LicenseUrl", "IconUrl", "Rating",
				"RatingsCount", "DownloadCount", "Categories", "Tags", "Dependencies", "ReportAbuseUrl", "GalleryDetailsUrl"
			}.ForEach(s => package.PropertyNames.ShouldContain(s));

			package.Properties["Id"].Type.ToString().ShouldEqual("Edm.String");
			package.Properties["Id"].IsNullable.ShouldBeFalse();

			package.Properties["Authors"].Type.ToString().ShouldEqual("Edm.String");
			package.Properties["Authors"].IsNullable.ShouldBeTrue();

			package.Properties["PackageSize"].Type.ToString().ShouldEqual("Edm.Int64");
			package.Properties["PackageSize"].IsNullable.ShouldBeFalse();

			package.Properties["IsLatestVersion"].Type.ToString().ShouldEqual("Edm.Boolean");
			package.Properties["IsLatestVersion"].IsNullable.ShouldBeFalse();

			// <Key>
			package.Keys.Count.ShouldEqual(2);

			package.Keys.First().Name.ShouldEqual("Id");
			package.Keys.First().Type.ToString().ShouldEqual("Edm.String");
			package.Keys.First().IsNullable.ShouldBeFalse();

			package.Keys.Last().Name.ShouldEqual("Version");
			package.Keys.Last().Type.ToString().ShouldEqual("Edm.String");
			package.Keys.Last().IsNullable.ShouldBeFalse();

			// var screenshot = metadata.EntityTypes["PublishedScreenshot"];
			// ...

			// <Association>
			// metadata.Associations.Count.ShouldEqual(1);
			// ...

			// <EntityContainer>
			// metadata.EntityContainers.Count.ShouldEqual(1);
			// ...
		}

		[Test][Ignore]
		public void can_get_url_that_would_be_queried() {
		}
	}
}
