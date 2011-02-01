using System;
using System.Linq;
using System.Collections.Generic;
using Requestoring;
using EasyOData;
using EasyOData.Filters.Extensions; // gives us the _Equals() style extension methods
using NUnit.Framework;

namespace EasyOData.Specs {

	[TestFixture]
	public class DevExpressExampleSpec : Spec {

		string DevExpressServiceRoot = "http://www.pluralsight-training.net/Odata/";
		Service service;

		[SetUp]
		public void Before() {
			base.Before();
			FakeResponse(DevExpressServiceRoot,                           "DevExpress", "root.xml");
			FakeResponse(DevExpressServiceRoot + "$metadata",             "DevExpress", "metadata.xml");
			FakeResponse(DevExpressServiceRoot + "Video?$top=2",          "DevExpress", "Video_top_2.xml");
			service = new Service(DevExpressServiceRoot);
		}

		[Test][Ignore]
		public void can_get_top_2_videos() {
			var videos = service["Videos"].Top(2);

			videos.Count.ShouldEqual(2);

			videos.First()["Oid"].ToString().ShouldEqual("bdc4e09e-4120-4c5a-8890-f71c040a8ba");
			videos.Last()["Oid"].ToString().ShouldEqual("3f75a1fa-dc6a-4a4c-8936-516307cb78ba");

			// should _actually_ be a Guid ...
		}

		[Test][Ignore]
		public void can_get_collection_names() {
		}

		[Test][Ignore]
		public void can_get_video_by_GUID_id() {
		}

		[Test][Ignore]
		public void can_get_a_comment() {
		}

		[Test][Ignore]
		public void can_get_a_comments_video() {
		}

		[Test][Ignore]
		public void can_get_a_videos_comments() {
		}

		[Test][Ignore]
		public void can_get_metadata() {
		}
	}
}
