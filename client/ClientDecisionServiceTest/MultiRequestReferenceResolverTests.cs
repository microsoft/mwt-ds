using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class CachingReferenceResolverTests
    {
        [TestMethod]
        [TestCategory("Client Library")]
        [Priority(0)]
        public void TestCachingReferenceResolver_Size()
        {
            var resolver = new CachingReferenceResolver(2);

            var s1 = new Sub { Id = 1, X = 1 };
            var s2 = new Sub { Id = 2, X = 2 };
            var s3 = new Sub { Id = 3, X = 3 };

            var d = new Data { Name = "foo", Subs = new List<Sub>{ s1, s2 } };

            var json = JsonConvert.SerializeObject(d, Formatting.None, new JsonSerializerSettings { ReferenceResolverProvider = () => resolver });
            var jobject = JsonConvert.DeserializeObject(json) as JObject;

            Assert.AreEqual("foo", jobject.Value<string>("Name"));

            var subs = jobject.Value<JArray>("Subs");
            var s1_j = subs[0];
            var s2_j = subs[1];
            var guid1 = s1_j.Value<string>("$id");
            var guid2 = s2_j.Value<string>("$id");

            Assert.AreEqual(s1.Id, s1_j.Value<int>("Id"));
            Assert.AreEqual(s1.X, s1_j.Value<float>("X"));

            Assert.AreEqual(s2.Id, s2_j.Value<int>("Id"));
            Assert.AreEqual(s2.X, s2_j.Value<float>("X"));

            Thread.Sleep(20);

            d = new Data { Name = "bar", Subs = new List<Sub>{ s2, s3 } };
            json = JsonConvert.SerializeObject(d, Formatting.None, new JsonSerializerSettings { ReferenceResolverProvider = () => resolver });
            jobject = JsonConvert.DeserializeObject(json) as JObject;

            Assert.AreEqual("bar", jobject.Value<string>("Name"));
            subs = jobject.Value<JArray>("Subs");
            s2_j = subs[0];
            var s3_j = subs[1];

            Assert.AreEqual(guid2, s2_j.Value<string>("$ref"));

            Assert.AreEqual(s3.Id, s3_j.Value<int>("Id"));
            Assert.AreEqual(s3.X, s3_j.Value<float>("X"));

            // make sure the right values are removed
            Thread.Sleep(20);

            d = new Data { Name = "zoo", Subs = new List<Sub>{ s1 } };
            json = JsonConvert.SerializeObject(d, Formatting.None, new JsonSerializerSettings { ReferenceResolverProvider = () => resolver });
            jobject = JsonConvert.DeserializeObject(json) as JObject;

            Assert.AreEqual("zoo", jobject.Value<string>("Name"));
            subs = jobject.Value<JArray>("Subs");
            s1_j = subs[0];

            Assert.AreNotEqual(s1_j.Value<string>("$ref"), guid1);
        }

        [TestMethod]
        [TestCategory("Client Library")]
        [Priority(0)]
        public void TestCachingReferenceResolver_Age()
        {
            var resolver = new CachingReferenceResolver(TimeSpan.FromSeconds(1));

            var s1 = new Sub { Id = 1, X = 1 };
            var s2 = new Sub { Id = 2, X = 2 };
            var s3 = new Sub { Id = 3, X = 3 };

            var d = new Data { Name = "foo", Subs = new List<Sub> { s1, s2 } };

            var json = JsonConvert.SerializeObject(d, Formatting.None, new JsonSerializerSettings { ReferenceResolverProvider = () => resolver });
            var jobject = JsonConvert.DeserializeObject(json) as JObject;

            Assert.AreEqual("foo", jobject.Value<string>("Name"));

            var subs = jobject.Value<JArray>("Subs");
            var s1_j = subs[0];
            var s2_j = subs[1];
            var guid1 = s1_j.Value<string>("$id");
            var guid2 = s2_j.Value<string>("$id");

            Assert.AreEqual(s1.Id, s1_j.Value<int>("Id"));
            Assert.AreEqual(s1.X, s1_j.Value<float>("X"));

            Assert.AreEqual(s2.Id, s2_j.Value<int>("Id"));
            Assert.AreEqual(s2.X, s2_j.Value<float>("X"));

            d = new Data { Name = "bar", Subs = new List<Sub> { s2, s3 } };
            json = JsonConvert.SerializeObject(d, Formatting.None, new JsonSerializerSettings { ReferenceResolverProvider = () => resolver });
            jobject = JsonConvert.DeserializeObject(json) as JObject;

            Assert.AreEqual("bar", jobject.Value<string>("Name"));
            subs = jobject.Value<JArray>("Subs");
            s2_j = subs[0];
            var s3_j = subs[1];

            Assert.AreEqual(guid2, s2_j.Value<string>("$ref"));

            Assert.AreEqual(s3.Id, s3_j.Value<int>("Id"));
            Assert.AreEqual(s3.X, s3_j.Value<float>("X"));

            Thread.Sleep(TimeSpan.FromSeconds(1.5));

            d = new Data { Name = "zoo", Subs = new List<Sub> { s1 } };
            json = JsonConvert.SerializeObject(d, Formatting.None, new JsonSerializerSettings { ReferenceResolverProvider = () => resolver });
            jobject = JsonConvert.DeserializeObject(json) as JObject;

            Assert.AreEqual("zoo", jobject.Value<string>("Name"));
            subs = jobject.Value<JArray>("Subs");
            s1_j = subs[0];

            Assert.AreNotEqual(s1_j.Value<string>("$ref"), guid1);
        }

        [TestMethod]
        [TestCategory("Client Library")]
        [Priority(0)]
        public void TestCachingReferenceResolver_CustomComparer()
        {
            var resolver = new CachingReferenceResolver(equalityComparer: new SubComparer());

            var s1 = new Sub { Id = 1, X = 1 };
            var s2 = new Sub { Id = 2, X = 2 };
            var s3 = new Sub { Id = 3, X = 3 };

            var d = new Data { Name = "foo", Subs = new List<Sub> { s1, s2 } };

            var json = JsonConvert.SerializeObject(d, Formatting.None, new JsonSerializerSettings { ReferenceResolverProvider = () => resolver });
            var jobject = JsonConvert.DeserializeObject(json) as JObject;

            Assert.AreEqual("foo", jobject.Value<string>("Name"));

            var subs = jobject.Value<JArray>("Subs");
            var s1_j = subs[0];
            var s2_j = subs[1];
            var guid1 = s1_j.Value<string>("$id");
            var guid2 = s2_j.Value<string>("$id");

            Assert.AreEqual(s1.Id, s1_j.Value<int>("Id"));
            Assert.AreEqual(s1.X, s1_j.Value<float>("X"));

            Assert.AreEqual(s2.Id, s2_j.Value<int>("Id"));
            Assert.AreEqual(s2.X, s2_j.Value<float>("X"));

            s2 = new Sub { Id = 2, X = 2 };

            d = new Data { Name = "bar", Subs = new List<Sub> { s2, s3 } };
            json = JsonConvert.SerializeObject(d, Formatting.None, new JsonSerializerSettings { ReferenceResolverProvider = () => resolver });
            jobject = JsonConvert.DeserializeObject(json) as JObject;

            Assert.AreEqual("bar", jobject.Value<string>("Name"));
            subs = jobject.Value<JArray>("Subs");
            s2_j = subs[0];
            var s3_j = subs[1];

            Assert.AreEqual(guid2, s2_j.Value<string>("$ref"));

            Assert.AreEqual(s3.Id, s3_j.Value<int>("Id"));
            Assert.AreEqual(s3.X, s3_j.Value<float>("X"));

            Thread.Sleep(TimeSpan.FromSeconds(1.5));

            d = new Data { Name = "zoo", Subs = new List<Sub> { s1 } };
            json = JsonConvert.SerializeObject(d, Formatting.None, new JsonSerializerSettings { ReferenceResolverProvider = () => resolver });
            jobject = JsonConvert.DeserializeObject(json) as JObject;

            Assert.AreEqual("zoo", jobject.Value<string>("Name"));
            subs = jobject.Value<JArray>("Subs");
            s1_j = subs[0];

            Assert.AreEqual(s1_j.Value<string>("$ref"), guid1);
        }

        [TestMethod]
        [TestCategory("Client Library")]
        [Priority(0)]
        public void TestCachingReferenceResolver()
        {
            var resolver = new CachingReferenceResolver();

            var s1 = new Sub { Id = 1, X = 1 };
            var s2 = new Sub { Id = 2, X = 2 };
            var s3 = new Sub { Id = 3, X = 3 };

            var d = new Data { Name = "foo", Subs = new List<Sub> { s1, s2 } };

            var json = JsonConvert.SerializeObject(d, Formatting.None, new JsonSerializerSettings { ReferenceResolverProvider = () => resolver });
            var jobject = JsonConvert.DeserializeObject(json) as JObject;

            Assert.AreEqual("foo", jobject.Value<string>("Name"));

            // make sure non-annotated Data class doesn't get Ids
            Assert.IsNull(jobject.Value<string>("$id"));

            var subs = jobject.Value<JArray>("Subs");
            var s1_j = subs[0];
            var s2_j = subs[1];
            var guid1 = s1_j.Value<string>("$id");
            var guid2 = s2_j.Value<string>("$id");

            Assert.AreEqual(s1.Id, s1_j.Value<int>("Id"));
            Assert.AreEqual(s1.X, s1_j.Value<float>("X"));

            Assert.AreEqual(s2.Id, s2_j.Value<int>("Id"));
            Assert.AreEqual(s2.X, s2_j.Value<float>("X"));

            d = new Data { Name = "bar", Subs = new List<Sub> { s2, s3 } };
            json = JsonConvert.SerializeObject(d, Formatting.None, new JsonSerializerSettings { ReferenceResolverProvider = () => resolver });
            jobject = JsonConvert.DeserializeObject(json) as JObject;

            Assert.AreEqual("bar", jobject.Value<string>("Name"));
            subs = jobject.Value<JArray>("Subs");
            s2_j = subs[0];
            var s3_j = subs[1];

            Assert.AreEqual(guid2, s2_j.Value<string>("$ref"));

            Assert.AreEqual(s3.Id, s3_j.Value<int>("Id"));
            Assert.AreEqual(s3.X, s3_j.Value<float>("X"));

            Thread.Sleep(TimeSpan.FromSeconds(1.5));

            d = new Data { Name = "zoo", Subs = new List<Sub> { s1 } };
            json = JsonConvert.SerializeObject(d, Formatting.None, new JsonSerializerSettings { ReferenceResolverProvider = () => resolver });
            jobject = JsonConvert.DeserializeObject(json) as JObject;

            Assert.AreEqual("zoo", jobject.Value<string>("Name"));
            subs = jobject.Value<JArray>("Subs");
            s1_j = subs[0];

            Assert.AreEqual(s1_j.Value<string>("$ref"), guid1);
        }

        [TestMethod]
        [TestCategory("Client Library")]
        [Priority(0)]
        public void TestCachingReferenceResolver_NotSupportedDeserialization_Id()
        {
            try
            {
                JsonConvert.DeserializeObject<Sub>("{\"$id\":\"1\"}", new JsonSerializerSettings { ReferenceResolverProvider = () => new CachingReferenceResolver() });
                Assert.Fail("Expected JsonSerializationException");
            }
            catch (NotSupportedException)
            {
                // implementation detail of JSON.NET if they're wrapping or not
            }
            catch (JsonSerializationException jse)
            {
                Assert.IsInstanceOfType(jse.InnerException, typeof(NotSupportedException));
            }
        }

        [TestMethod]
        [TestCategory("Client Library")]
        [Priority(0)]
        public void TestCachingReferenceResolver_NotSupportedDeserialization_Ref()
        {
            try
            {
                JsonConvert.DeserializeObject<Sub>("{\"$ref\":\"1\"}", new JsonSerializerSettings { ReferenceResolverProvider = () => new CachingReferenceResolver() });
                Assert.Fail("Expected JsonSerializationException");
            }
            catch (NotSupportedException)
            {
                // implementation detail of JSON.NET if they're wrapping or not
            }
            catch (JsonSerializationException jse)
            {
                Assert.IsInstanceOfType(jse.InnerException, typeof(NotSupportedException));
            }
        }
    }

    public class Data
    {
        public string Name { get; set; }
        
        // [JsonObject(ItemIsReference=true)]
        public List<Sub> Subs { get; set; }
    }

    [JsonObject(IsReference=true)]
    public class Sub
    {
        public int Id { get; set; }

        public float X { get; set; }
    }


    public class SubComparer : IEqualityComparer<object>
    {
        public new bool Equals(object x, object y)
        {
            return ((Sub)x).Id == ((Sub)y).Id;
        }

        public int GetHashCode(object obj)
        {
            return -1;
        }
    }
}
