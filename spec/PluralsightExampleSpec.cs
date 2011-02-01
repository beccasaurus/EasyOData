using System;
using System.Linq;
using System.Collections.Generic;
using Requestoring;
using EasyOData;
using EasyOData.Filters.Extensions; // gives us the _Equals() style extension methods
using NUnit.Framework;

namespace EasyOData.Specs {

	[TestFixture]
	public class PluralsightExampleSpec : Spec {

		string PluralsightServiceRoot = "http://www.pluralsight-training.net/Odata/";
		Service service;

		[SetUp]
		public void Before() {
			base.Before();
			FakeResponse(PluralsightServiceRoot,                             "Pluralsight", "root.xml");
			FakeResponse(PluralsightServiceRoot + "$metadata",               "Pluralsight", "metadata.xml");
			FakeResponse(PluralsightServiceRoot + "Courses?$top=1",          "Pluralsight", "Courses_top_1.xml");
			FakeResponse(PluralsightServiceRoot + "Courses('Agile%20Team%20Practices%20with%20Scrum')", "Pluralsight", "Courses_get_using_title.xml");
			FakeResponse(PluralsightServiceRoot + "Courses('foo')",          "Pluralsight", "Courses_get_using_title_non_existent.xml");
			service = new Service(PluralsightServiceRoot);
		}

		[Test]
		public void can_get_course_by_key() {
			var course = service["Courses"].Get("Agile Team Practices with Scrum");

			course["Title"].ShouldEqual("Agile Team Practices with Scrum");
			course["Name"].ShouldEqual("agile-team-practice-fundamentals");
		}

		[Test]
		public void can_get_null_course_by_ninexistent_key() {
			service["Courses"].Get("foo").Should(Be.Null);
		}

		[Test]
		public void can_get_top_1_course() {
			var course = service.Collections["Courses"].Top(1).First;

			course["Title"].ShouldEqual(".NET Distributed Systems Architecture");
			course["Description"].ToString().ShouldContain("on the Microsoft platform. The course looks at the general knowledge one needs prior");
			course["VideoLength"].ShouldEqual("05:29:48");
			course["Category"].ShouldEqual("WCF");
			course["Name"].ShouldEqual("dotnet-distributed-architecture");
			course["ShortDescription"].ShouldEqual("This course provides an overview of the architecture and technology used to build distributed\n\tsytems on the Microsoft platform.\n    ");
		}

		[Test]
		public void can_get_collection_names() {
			service.CollectionNames.ShouldEqual(new List<string> { "Categories", "Courses", "HowTos", "Modules", "Tutorials" });
		}

		[Test]
		public void can_get_metadata() {
			var baseType = service.EntityTypes["ModelItemBase"];
			baseType.FullName.ShouldEqual("Pluralsight.OData.Model.ModelItemBase");
			baseType.BaseTypeName.Should(Be.Null);
			baseType.BaseType.Should(Be.Null);
			baseType.PropertyNames.ShouldEqual(new List<string>{ "Category", "Description", "Title", "VideoLength" });

			var course = service.EntityTypes["Course"];
			course.BaseTypeName.ShouldEqual("Pluralsight.OData.Model.ModelItemBase");
			course.BaseType.ShouldEqual(baseType);
			course.CorePropertyNames.ShouldEqual(new List<string>{ "Name", "ShortDescription" }); // Core ... just this type's properties
			course.BasePropertyNames.ShouldEqual(new List<string>{ "Category", "Description", "Title", "VideoLength" }); // Base ... just properties from BaseType
			course.PropertyNames.ShouldEqual(new List<string>{ "Category", "Description", "Name", "ShortDescription", "Title", "VideoLength" }); // Combined
		}
	}
}
