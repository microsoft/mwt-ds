using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Analytics.Interfaces;
using Microsoft.Analytics.UnitTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DecisionServiceExtractor.Test
{
    [TestClass]
    public class JsonParserTest
    {
        private ISchema CreateTestSchema()
        {
            return new USqlSchema(
                new USqlColumn<string>("EventId"),
                new USqlColumn<DateTime>("Timestamp"),
                new USqlColumn<float>("Cost"),
                new USqlColumn<float>("Prob"),
                new USqlColumn<int>("Action"),
                new USqlColumn<int>("NumActions"),
                new USqlColumn<int>("HasObservations"),
                new USqlColumn<string>("Data")
                );
        }

        private IRow CreateDefaultRow(ISchema schema)
        {
            var objects = new object[schema.Count];
            for (int i = 0; i < schema.Count; ++i)
            {
                objects[i] = schema[i].DefaultValue;
            }
            return new USqlRow(schema, objects);
        }


        [TestMethod]
        public void ActionCountTest()
        {
            var example = @"{""_label_cost"":-2,""_label_probability"":0.911111116,""_label_Action"":8,""_labelIndex"":7,""o"":[{""EventId"":""6c33a7c07aae4868b4b14f944e3320c0"",""v"":1},{""EventId"":""6c33a7c07aae4868b4b14f944e3320c0"",""v"":0},{""EventId"":""6c33a7c07aae4868b4b14f944e3320c0"",""v"":2}],""Timestamp"":""2018-07-19T23:30:57.6700000Z"",""Version"":""1"",""EventId"":""6c33a7c07aae4868b4b14f944e3320c0"",""a"":[8,1,6,2,10,11,9,5,3,7,4],""c"":{""_synthetic"":false,""User"":{""_age"":0},""Geo"":{""country"":""United States"",""_countrycf"":""8"",""state"":""Nevada"",""city"":""Las Vegas"",""_citycf"":""8"",""dma"":""839""},""MRefer"":{""referer"":""https://www.complex.com/""},""OUserAgent"":{""_ua"":""Mozilla/5.0 (Macintosh; Intel Mac OS X 10_10_5) AppleWebKit/603.3.8 (KHTML, like Gecko) Version/10.1.2 Safari/603.3.8"",""_DeviceBrand"":"""",""_DeviceFamily"":""Other"",""_DeviceIsSpider"":false,""_DeviceModel"":"""",""_OSFamily"":""Mac OS X"",""_OSMajor"":""10"",""_OSPatch"":""5"",""DeviceType"":""Desktop""},""_multi"":[{""_tag"":""cmplx$http://www.complex.com/music/2018/07/four-men-indicted-for-fatally-shooting-xxxtentacion"",""i"":{""constant"":1,""id"":""cmplx$http://www.complex.com/music/2018/07/four-men-indicted-for-fatally-shooting-xxxtentacion""},""j"":[{""_title"":""Four Men Indicted by Grand Jury for Fatally Shooting XXXTentacion""},null,{""Emotion0"":{""anger"":0.08193103,""contempt"":0.0142859118,""disgust"":0.0046643177,""fear"":0.000101052363,""happiness"":5.75684571E-05,""neutral"":0.8652449,""sadness"":0.0334045,""surprise"":0.000310689618},""Emotion1"":{""anger"":0.0361110829,""contempt"":0.008100673,""disgust"":0.000575012062,""fear"":6.130771E-05,""happiness"":5.01265458E-05,""neutral"":0.945477247,""sadness"":0.008256853,""surprise"":0.00136769784},""Emotion2"":{""anger"":0.0251881052,""contempt"":0.0003558025,""disgust"":0.00133104972,""fear"":0.00114253594,""happiness"":0.947571039,""neutral"":0.013514352,""sadness"":0.00408442831,""surprise"":0.006812664},""_expires"":""2018-07-22T22:24:33.6560076Z""},null,{""XSentiment"":0.5,""_expires"":""2018-07-22T22:24:33.6390641Z""},null,null]},{""_tag"":""cmplx$http://www.complex.com/music/2018/07/2-chainz-on-his-pink-trap-house-destruction-i-dont-think-they-would-known-down-the-statue-of-liberty"",""i"":{""constant"":1,""id"":""cmplx$http://www.complex.com/music/2018/07/2-chainz-on-his-pink-trap-house-destruction-i-dont-think-they-would-known-down-the-statue-of-liberty""},""j"":[{""_title"":""2 Chainz on His Pink Trap House's Destruction: 'I Don’t Think They Would Knock Down the Statue of Li""},null,{""_expires"":""2018-07-22T18:39:06.435326Z""},null,{""XSentiment"":0.5,""_expires"":""2018-07-22T18:39:06.437968Z""},null,null]},{""_tag"":""cmplx$http://www.complex.com/sports/2018/07/watch-chloe-kim-rap-cardi-b-part-in-no-limit-with-g-eazy"",""i"":{""constant"":1,""id"":""cmplx$http://www.complex.com/sports/2018/07/watch-chloe-kim-rap-cardi-b-part-in-no-limit-with-g-eazy""},""j"":[{""_title"":""Watch Chloe Kim Rap Cardi B’s Part in “No Limit” With G-Eazy""},null,{""Emotion0"":{""anger"":5.271395E-10,""contempt"":1.18699287E-11,""disgust"":1.64511382E-09,""fear"":1.06379437E-11,""happiness"":1,""neutral"":7.029596E-10,""sadness"":7.483194E-10,""surprise"":1.54087343E-09},""_expires"":""2018-07-22T21:38:36.3179955Z""},null,{""XSentiment"":0.5,""_expires"":""2018-07-22T21:38:36.1764128Z""},null,null]},{""_tag"":""cmplx$http://www.complex.com/pop-culture/2018/07/the-best-will-smith-movies"",""i"":{""constant"":1,""id"":""cmplx$http://www.complex.com/pop-culture/2018/07/the-best-will-smith-movies""},""j"":[{""_title"":""The Best Will Smith Movies""},null,{""Emotion0"":{""anger"":0.00134275958,""contempt"":0.01697544,""disgust"":0.0023304366,""fear"":9.310076E-05,""happiness"":0.5494229,""neutral"":0.421826631,""sadness"":0.00700154155,""surprise"":0.0010071845},""_expires"":""2018-07-22T21:09:36.1175959Z""},null,{""XSentiment"":0.9716126,""_expires"":""2018-07-22T21:09:35.8308744Z""},null,null]},{""_tag"":""cmplx$https://www.complex.com/sports/2018/07/2018-espys-red-carpet"",""i"":{""constant"":1,""id"":""cmplx$https://www.complex.com/sports/2018/07/2018-espys-red-carpet""},""j"":[{""_title"":""Professional Athletes Keep It Real About 'Loyalty' at the 2018 ESPYs""},null,{""Emotion0"":{""anger"":3.38298159E-05,""contempt"":2.0690366E-05,""disgust"":5.13375526E-05,""fear"":4.964087E-06,""happiness"":0.9976058,""neutral"":0.00200294587,""sadness"":0.000260378147,""surprise"":2.00380746E-05},""_expires"":""2018-07-22T22:41:52.3964008Z""},null,{""XSentiment"":0.5,""_expires"":""2018-07-22T22:41:52.0057354Z""},null,null]},{""_tag"":""cmplx$https://www.complex.com/sports/2018/07/jimmy-garoppolo-spotted-with-adult-film-star-kiara-mia"",""i"":{""constant"":1,""id"":""cmplx$https://www.complex.com/sports/2018/07/jimmy-garoppolo-spotted-with-adult-film-star-kiara-mia""},""j"":[{""_title"":""Jimmy Garoppolo Spotted in Beverly Hills With Adult Film Star Kiara Mia""},null,{""_expires"":""2018-07-22T21:34:08.4969709Z""},{""_expires"":""2018-07-19T23:09:18.243512Z""},{""XSentiment"":0.5,""_expires"":""2018-07-22T21:34:08.5229152Z""},null,null]},{""_tag"":""cmplx$https://www.complex.com/pop-culture/2018/07/joji-and-rich-brian-play-the-newlywed-game-while-eating-spicy-wings-hot-ones"",""i"":{""constant"":1,""id"":""cmplx$https://www.complex.com/pop-culture/2018/07/joji-and-rich-brian-play-the-newlywed-game-while-eating-spicy-wings-hot-ones""},""j"":[{""_title"":""Joji and Rich Brian Play the Newlywed Game While Eating Spicy Wings | Hot Ones""},null,{""Emotion0"":{""anger"":0.000149607382,""contempt"":0.00592999766,""disgust"":0.000307883281,""fear"":6.14888359E-06,""happiness"":0.05696309,""neutral"":0.9336905,""sadness"":0.00251298165,""surprise"":0.000439826224},""Emotion1"":{""anger"":1.47376359E-05,""contempt"":0.000332679017,""disgust"":3.11678159E-05,""fear"":6.127358E-08,""happiness"":0.9778741,""neutral"":0.0217296854,""sadness"":4.953524E-06,""surprise"":1.26064178E-05},""_expires"":""2018-07-22T15:08:54.8660979Z""},null,{""XSentiment"":0.5,""_expires"":""2018-07-22T15:08:54.3421013Z""},null,null]},{""_tag"":""cmplx$https://www.complex.com/music/2018/07/kid-cudi-says-he-and-kanye-west-are-planning-to-do-more-kids-see-ghosts-albums"",""i"":{""constant"":1,""id"":""cmplx$https://www.complex.com/music/2018/07/kid-cudi-says-he-and-kanye-west-are-planning-to-do-more-kids-see-ghosts-albums""},""j"":[{""_title"":""Kid Cudi Says He and Kanye West Are Planning to Do More 'Kids See Ghosts' Albums""},null,{""Emotion0"":{""anger"":0.004779356,""contempt"":0.0396654122,""disgust"":0.000898613245,""fear"":6.362816E-05,""happiness"":0.0329472534,""neutral"":0.9089038,""sadness"":0.01218673,""surprise"":0.000555203645},""Emotion1"":{""anger"":0.0016001676,""contempt"":0.00258500734,""disgust"":0.0004716456,""fear"":2.961242E-05,""happiness"":0.000576888851,""neutral"":0.9908969,""sadness"":0.0006469234,""surprise"":0.00319288624},""_expires"":""2018-07-22T19:13:10.3669958Z""},null,{""XSentiment"":0.5,""_expires"":""2018-07-22T19:13:10.2018036Z""},null,null]},{""_tag"":""cmplx$https://www.complex.com/pop-culture/2018/07/chance-drops-some-heat-chief-keef-hologram-tour-genius-or-lazy-everyday-struggle"",""i"":{""constant"":1,""id"":""cmplx$https://www.complex.com/pop-culture/2018/07/chance-drops-some-heat-chief-keef-hologram-tour-genius-or-lazy-everyday-struggle""},""j"":[{""_title"":""Chance Drops Some Heat, Chief Keef Hologram Tour Genius or Lazy? | Everyday Struggle ""},null,{""Emotion0"":{""anger"":0.008510953,""contempt"":0.004868189,""disgust"":0.00184451311,""fear"":0.0329660736,""happiness"":0.138749734,""neutral"":0.743005753,""sadness"":0.0405789427,""surprise"":0.0294758677},""_expires"":""2018-07-22T15:02:40.6014988Z""},null,{""XSentiment"":0.5,""_expires"":""2018-07-22T15:02:40.5104894Z""},null,null]}]},""p"":[0.9111111,0.0111111114,0.0111111114,0.0111111114,0.0111111114,0.0111111114,0.0111111114,0.0111111114,0.0111111114,0.0111111114,0.0111111114],""VWState"":{""m"":""5744c0fe554e462c8e300670c9029102-1Nhzo/59b51c004d714814b2d81313dc189440-1NhEl""}}";
            var extractor = new HeaderOnly();
            var output = new USqlUpdatableRow(CreateDefaultRow(CreateTestSchema()));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(example)))
            {
                var input = new USqlStreamReader(stream);
                foreach (var outputRow in extractor.Extract(input, output))
                {
                    Assert.AreEqual("6c33a7c07aae4868b4b14f944e3320c0", output.Get<string>("EventId"));
                    Assert.AreEqual(-2.0, output.Get<float>("Cost"), 1e-6);
                    Assert.AreEqual(0.911111116, output.Get<float>("Prob"), 1e-6);
                    Assert.AreEqual(11, output.Get<int>("NumActions"));
                    Assert.AreEqual(8, output.Get<int>("Action"));
                    Assert.AreEqual(1, output.Get<int>("HasObservations"));
                }
            }
        }
    }
}
