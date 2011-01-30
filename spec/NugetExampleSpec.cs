using System;
using System.Linq;
using Requestoring;
using EasyOData;
using NUnit.Framework;

namespace EasyOData.Specs {

	[TestFixture]
	public class NugetExampleSpec : Spec {

		static string NuGetServiceRoot = "http://packages.nuget.org/v1/FeedService.svc/";
		Service service                = new Service(NuGetServiceRoot);

		[SetUp]
		public void Before() {
			base.Before();
			FakeResponse(NuGetServiceRoot, "NuGet", "root.xml");
		}

		[Test]
		public void can_get_collection_names() {
			service.CollectionNames.Count.ShouldEqual(2);
			service.CollectionNames.ShouldContain("Packages");
			service.CollectionNames.ShouldContain("Screenshots");
		}

		[Test][Ignore]
		public void can_get_collections() {
			service.Collections.Count.ShouldEqual(2);
			service.Collections.First().Name.ShouldEqual("Packages");
			service.Collections.First().Href.ShouldEqual("Packages");
			service.Collections.Last().Name.ShouldEqual("Screenshots");
			service.Collections.Last().Href.ShouldEqual("Screenshots");
		}
	}
}
