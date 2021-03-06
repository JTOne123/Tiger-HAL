using FsCheck;
using FsCheck.Xunit;
using Newtonsoft.Json;
using Test.Utility;
using Tiger.Hal;
using Xunit;

namespace Test
{
    /// <summary>Tests related to the <see cref="Link"/> class.</summary>
    [Properties(Arbitrary = new[] { typeof(Generators) }, QuietOnSuccess = true, MaxTest = 0x400)]
    public static class LinkTests
    {
        [Property(DisplayName = "A link survives serialization.")]
        public static void Serialization_RoundTrip(Link link, JsonSerializerSettings serializerSettings)
        {
            var actual = JsonConvert.DeserializeObject<Link>(
                JsonConvert.SerializeObject(link, serializerSettings),
                serializerSettings);

            Assert.Equal(link, actual, new LinkEqualityComparer());
        }
    }
}
