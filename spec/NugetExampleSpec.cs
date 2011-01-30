using System;
using System.Linq;
using System.Collections.Generic;
using Requestoring;
using EasyOData;
using NUnit.Framework;

namespace EasyOData.Specs {

	[TestFixture]
	public class NugetExampleSpec : Spec {

		// Note, this isn't specific to NugetExampleSpec and can be moved ...
		[Test]
		public void can_get_path_that_would_be_queried() {
			var comma = "%2c";

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
		}

		string NuGetServiceRoot = "http://packages.nuget.org/v1/FeedService.svc/";
		Service service;

		[SetUp]
		public void Before() {
			base.Before();
			FakeResponse(NuGetServiceRoot, "NuGet", "root.xml");
			FakeResponse(NuGetServiceRoot + "$metadata", "NuGet", "metadata.xml");
			service = new Service(NuGetServiceRoot);
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
