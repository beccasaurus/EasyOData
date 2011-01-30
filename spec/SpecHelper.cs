using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Requestoring;
using EasyOData;
using NUnit.Framework;

namespace EasyOData.Specs {

	public class Spec {

		[SetUp]
		public void Before() {
			Requestor.Global.Reset();
			Requestor.Global.DisableRealRequests();
		}

		public string SavedResponseDir {
			get { return Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "spec", "saved-responses"); }
		}

		public void FakeResponse(string getUrl, params string[] pathToSavedResponse) {
			var allParts = new List<string>(pathToSavedResponse);
			allParts.Insert(0, SavedResponseDir);
			var filePath = Path.Combine(allParts.ToArray());

			if (! File.Exists(filePath))
				throw new Exception(string.Format("Couldn't find saved response: {0}", filePath));
			
			Requestor.Global.FakeResponse("GET", getUrl, Response.FromHttpResponse(File.ReadAllText(filePath)));
		}
	}
}
