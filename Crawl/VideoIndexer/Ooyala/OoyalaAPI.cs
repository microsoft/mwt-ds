/*

Copyright 2011 © Ooyala, Inc.  All rights reserved.

Ooyala, Inc. (“Ooyala”) hereby grants permission, free of charge, to any person or entity obtaining a copy of the software code provided in source code format via this webpage and direct links contained within this webpage and any associated documentation (collectively, the "Software"), to use, copy, modify, merge, and/or publish the Software and, subject to pass-through of all terms and conditions hereof, permission to transfer, distribute and sublicense the Software; all of the foregoing subject to the following terms and conditions:

1.  The above copyright notice and this permission notice shall be included in all copies or portions of the Software.

2.   For purposes of clarity, the Software does not include any APIs, but instead consists of code that may be used in conjunction with APIs that may be provided by Ooyala pursuant to a separate written agreement subject to fees.  

3.   Ooyala may in its sole discretion maintain and/or update the Software.  However, the Software is provided without any promise or obligation of support, maintenance or update.  

4.  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, TITLE, AND NONINFRINGEMENT.  IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, RELATING TO, ARISING FROM, IN CONNECTION WITH, OR INCIDENTAL TO THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

5.   TO THE MAXIMUM EXTENT PERMITTED BY APPLICABLE LAW, (i) IN NO EVENT SHALL OOYALA BE LIABLE FOR ANY CONSEQUENTIAL, INCIDENTAL, INDIRECT, SPECIAL, PUNITIVE, OR OTHER DAMAGES WHATSOEVER (INCLUDING, WITHOUT LIMITATION, DAMAGES FOR LOSS OF BUSINESS PROFITS, BUSINESS INTERRUPTION, LOSS OF BUSINESS INFORMATION, OR OTHER PECUNIARY LOSS) RELATING TO, ARISING FROM, IN CONNECTION WITH, OR INCIDENTAL TO THE SOFTWARE OR THE USE OF OR INABILITY TO USE THE SOFTWARE, EVEN IF OOYALA HAS BEEN ADVISED OF THE POSSIBILITY OF SUCH DAMAGES, AND (ii) OOYALA’S TOTAL AGGREGATE LIABILITY RELATING TO, ARISING FROM, IN CONNECTION WITH, OR INCIDENTAL TO THE SOFTWARE SHALL BE LIMITED TO THE ACTUAL DIRECT DAMAGES INCURRED UP TO MAXIMUM AMOUNT OF FIFTY DOLLARS ($50).

 * */

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Net;
using System.IO;
using System.Collections;
using Procurios.Public;

namespace Ooyala.API
{
    public class OoyalaAPI
    {
        public const string BASE_URL = "https://api.ooyala.com/v2/";

        private const int UPLOAD_TIMEOUT = 1000 * 3600;

        public string apiKey;
        public string secretKey;

        private HttpWebResponse response;

        /// <summary>
        /// Default constructor. You need to provide your API key and secret key. You can find this in the developer tab in Backlot
        /// <seealso cref="http://api.ooyala.com/docs/v2/"/>
        /// </summary>
        /// <param name="apiKey">
        /// Your account's API key 
        /// </param>
        /// <param name="secretKey">
        /// Your account's secret key 
        /// </param>
        public OoyalaAPI(string apiKey, string secretKey)
        {
            this.apiKey = apiKey;
            this.secretKey = secretKey;
        }

        /// <summary>
        /// Makes an HTTP GET request to the Ooyala API with the specified path and using the given parameters
        /// </summary>
        /// <param name="path">
        /// The path to the resource to use for the GET request, i.e "players"
        /// </param>
        /// <param name="parameters">
        /// A Dictionary containing the parameters to be send along with the GET request. 
        /// The only required parameter is expires since api_key will be added for you with the value you passed in the constructor of this class
        /// </param>
        public Object get(string path, Dictionary<String, String> parameters)
        {
            var url = this.generateURL("GET", path, parameters, "");
            HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
            request.Method = "GET";

            if (this.getResponse(request))
            {
                return JSON.JsonDecode(new StreamReader(response.GetResponseStream()).ReadToEnd());
            }

            return null;
        }


        /// <summary>
        /// Makes a POST request to the Ooyala V2 API. Post request are used to create objects like Assets, Labels, Players, etc.
        /// </summary>
        /// <param name="path">
        /// The path to the resource to post to.
        /// </param>
        /// <param name="parameters">
        /// A Hash containing a list of parameters. The expires parameter is required. 
        /// </param>
        /// <param name="body">
        /// A String containing the JSON data to use for the creation of the object
        /// </param>
        /// <returns>
        /// returns the create object data in JSON format
        /// </returns>
        public Object post(string path, Dictionary<String, String> parameters, Hashtable body)
        {
            String jsonBody = JSON.JsonEncode(body);
            var url = this.generateURL("POST", path, parameters, jsonBody);

            HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
            request.Method = "POST";

            var data = Encoding.Default.GetBytes(jsonBody);

            request.ContentLength = data.Length;

            var stream = request.GetRequestStream();

            stream.Write(data, 0, data.Length);

            stream.Close();

            if (this.getResponse(request))
            {
                return JSON.JsonDecode(new StreamReader(response.GetResponseStream()).ReadToEnd());
            }

            return null;
        }

        /// <summary>
        /// Makes a POST request sending bytes encoded in UTF8 to the Ooyala V2 API. This method can be used to upload preview images or closed caption files for assets.
        /// </summary>
        /// <param name="path">
        /// The path to the resource to post the bytes to.
        /// </param>
        /// <param name="parameters">
        /// A Hash containing parameters to be sent along with the request. Required parameter: expires.
        /// </param>
        /// <param name="body">
        /// An array of bytes representing the image/file you want to post to the API object.
        /// </param>
        /// <returns>
        /// A String containing the JSON response from the Server
        /// </returns>
        public Object postBytes(string path, Dictionary<String, String> parameters, System.Byte[] body, String fileName = null)
        {
            var url = this.generateURL("POST", path, parameters, Encoding.Default.GetString(body));

            HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
            request.Method = "POST";
            request.AllowWriteStreamBuffering = false;
            request.Timeout = UPLOAD_TIMEOUT;
            request.SendChunked = false;

            request.ContentLength = body.Length;

            var stream = request.GetRequestStream();
            stream.Write(body, 0, body.Length);
            stream.Flush();
            stream.Close();

            if (this.getResponse(request))
            {
                return JSON.JsonDecode(new StreamReader(response.GetResponseStream()).ReadToEnd());
            }

            return null;
        }

        /// <summary>
        /// Puts the bytes from the specified file to the API resource.
        /// </summary>
        /// <returns>
        /// The JSON response from the server in the form of an Object.
        /// </returns>
        /// <param name='path'>
        /// The path to the resource to put the bytes to.
        /// </param>
        /// <param name='parameters'>
        /// Query string parameters in the form of a Dictionary.
        /// </param>
        /// <param name='body'>
        /// A Hashtable containing the body of the request that will later be converted into JSON.
        /// </param>
        /// <param name='fileName'>
        /// The name of the file whose bytes will be sent via the PUT request.
        /// </param>
        public Object putBytes(string path, Dictionary<String, String> parameters, System.Byte[] body, String fileName = null)
        {
            var url = this.generateURL("PUT", path, parameters, Encoding.Default.GetString(body));

            System.Net.Cache.RequestCachePolicy requestCachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

            HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
            request.CachePolicy = requestCachePolicy;
            request.Method = "PUT";
            request.AllowWriteStreamBuffering = false;
            request.Timeout = UPLOAD_TIMEOUT;
            request.SendChunked = false;

            request.ContentLength = body.Length;

            var stream = request.GetRequestStream();
            stream.Write(body, 0, body.Length);
            stream.Flush();
            stream.Close();

            if (this.getResponse(request))
            {
                return JSON.JsonDecode(new StreamReader(response.GetResponseStream()).ReadToEnd());
            }

            return null;
        }

        /// <summary>
        /// Make a PATCH request to the Ooyala V2 API. PATCH requests are used to update data on objects. <seealso cref="hhttp://api.ooyala.com/docs/v2"/>
        /// </summary>
        /// <param name="path">
        /// The path on the API server that indicates the object to be patched. In a patch request the path should look something like: :object_type/:object_id i.e. assets/zxhs16
        /// </param>
        /// <param name="parameters">
        /// A Hash containing the list of parameters to be sent along with the request. Required parameter: expires.
        /// </param>
        /// <param name="body">
        /// A String containing the data to be sent for the patch in JSON format.
        /// </param>
        /// <returns>
        /// A String containing the JSON response from the server.
        /// </returns>
        public Object patch(string path, Dictionary<String, String> parameters, Hashtable body)
        {
            var url = this.generateURL("PATCH", path, parameters, body);
            String jsonBody = JSON.JsonEncode(body);

            HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
            request.Method = "PATCH";
            request.ContentType = "application/x-www-form-urlencoded";

            var data = Encoding.UTF8.GetBytes(jsonBody);

            request.ContentLength = data.Length;

            var stream = request.GetRequestStream();

            stream.Write(data, 0, data.Length);

            stream.Close();

            if (this.getResponse(request))
            {
                return JSON.JsonDecode(new StreamReader(response.GetResponseStream()).ReadToEnd());
            }

            return null;
        }

        /// <summary>
        /// Make a PUT request to the Ooyala V2 API. PATCH requests are used to update data on objects. <seealso cref="http://api.ooyala.com/docs/v2"/>
        /// </summary>
        /// <param name="path">
        /// The path on the API server that indicates the object to be replaced. In a put request the path should look something like: :object_type/:object_id i.e. assets/zxhs16
        /// </param>
        /// <param name="parameters">
        /// A Hash containing the list of parameters to be sent along with the request. Required parameter: expires.
        /// </param>
        /// <param name="body">
        /// A String containing the data to be sent for the put in JSON format.
        /// </param>
        /// <returns>
        /// A String containing the JSON response from the server.
        /// </returns>
        public Object put(string path, Dictionary<String, String> parameters, Hashtable body)
        {
            var url = this.generateURL("PUT", path, parameters, body);

            String jsonBody = JSON.JsonEncode(body);

            HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
            request.Method = "PUT";
            request.ContentType = "application/x-www-form-urlencoded";

            var data = Encoding.UTF8.GetBytes(jsonBody);

            request.ContentLength = data.Length;

            var stream = request.GetRequestStream();

            stream.Write(data, 0, data.Length);

            stream.Close();

            if (this.getResponse(request))
            {
                return JSON.JsonDecode(new StreamReader(response.GetResponseStream()).ReadToEnd());
            }

            return null;
        }

        /// <summary>
        /// Issues a DELETE request to remove an object using the Ooyala V2 API. <seealso cref="http://api.ooyala.com/docs/v2"/>
        /// </summary>
        /// <param name="path">
        /// The path of the resource to delete
        /// </param>
        /// <param name="parameters">
        /// A Hash containing the list of parameters to be sent along with the request. Required parameter: expires.
        /// </param>
        /// <returns>
        /// True if the asset was deleted, false if it was already deleted or does not exist
        /// </returns>
        public Boolean delete(string path, Dictionary<String, String> parameters)
        {
            var url = this.generateURL("DELETE", path, parameters, "");

            HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
            request.Method = "DELETE";

            return this.getResponse(request);
        }

        private string generateURL(string HTTPMethod, string path, Dictionary<System.String, System.String> parameters, Hashtable body)
        {
            return generateURL(HTTPMethod, path, parameters, JSON.JsonEncode(body));
        }

        /// <summary>
        /// Takes in the necessary parameters to build a V2 signature for the Ooyala API
        /// </summary>
        /// <param name="HTTPMethod">
        /// The method to be used for the request. Possible values are: GET, POST, PUT, PATCH or DELETE
        /// </param>
        /// <param name="path">
        /// The path to use for the request
        /// </param>
        /// <param name="parameters">
        /// A hash containing the list of parameters that will be included in the request.
        /// </param>
        /// <param name="body">
        /// A string containing the JSON representation of the data to be sent on the request. If its a GET request, the body parameter will not be used to generate the signature.
        /// </param>
        /// <returns>
        /// The URL to be used in the HTTP request.
        /// </returns>
        private string generateURL(string HTTPMethod, string path, Dictionary<System.String, System.String> parameters, String body)
        {
            var url = BASE_URL + path;

            path = "/v2/" + path;

            if (!parameters.ContainsKey("api_key"))
            {
                parameters.Add("api_key", this.apiKey);
            }

            if (!parameters.ContainsKey("expires"))
            {
                DateTime now = DateTime.UtcNow;
                //Round up to the expiration to the next hour for higher caching performance
                DateTime expiresWindow = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
                expiresWindow = expiresWindow.AddHours(1);
                int expires = (int)(expiresWindow - new DateTime(1970, 1, 1)).TotalSeconds;
                parameters.Add("expires", expires.ToString());
            }

            //Sorting the keys
            var sortedKeys = new String[parameters.Keys.Count];
            parameters.Keys.CopyTo(sortedKeys, 0);
            Array.Sort(sortedKeys);

            for (int i = 0; i < sortedKeys.Length; i++)
            {
                url += (i == 0 && !url.Contains("?") ? "?" : "&") + sortedKeys[i] + "=" + HttpUtility.UrlEncode(parameters[sortedKeys[i]]);
            }

            url += "&signature=" + this.generateRequestSignature(HTTPMethod, path, sortedKeys, parameters, body);

            return url;
        }

        /// <summary>
        /// Generates the signature for the V2 API request based on the given parameters. <seealso cref="http://api.ooyala.com/v2/docs"/>
        /// </summary>
        /// <param name="HTTPMethod">
        /// The method to be used for the request. Possible values are: GET, POST, PUT, PATCH or DELETE
        /// </param>
        /// <param name="path">
        /// The path to use for the request
        /// </param>
        /// <param name="sortedParameterKeys">
        /// A sorted array containing the keys of the parameters hash. This is to improve efficiency and not sort them twice since generateURL already does it.
        /// </param>
        /// <param name="parameters">
        /// A hash containing the list of parameters that will be included in the request.
        /// </param>
        /// <param name="body">
        /// A string containing the JSON representation of the data to be sent on the request. If its a GET request, the body parameter will not be used to generate the signature.
        /// </param>
        /// <returns>
        /// A string containing the signature to be used in the V2 API request.
        /// </returns>
        internal string generateRequestSignature(string HTTPMethod, String path, String[] sortedParameterKeys, Dictionary<String, String> parameters, String body)
        {
            var stringToSign = this.secretKey + HTTPMethod + path;

            for (int i = 0; i < sortedParameterKeys.Length; i++)
            {
                stringToSign += sortedParameterKeys[i] + "=" + parameters[sortedParameterKeys[i]];
            }

            stringToSign += body;

            var sha256 = new SHA256Managed();
            byte[] digest = sha256.ComputeHash(Encoding.Default.GetBytes(stringToSign));
            string signedInput = Convert.ToBase64String(digest);

            //Removing the trailing = signs
            var lastEqualsSignindex = signedInput.Length - 1;
            while (signedInput[lastEqualsSignindex] == '=')
            {
                lastEqualsSignindex--;
            }

            signedInput = signedInput.Substring(0, lastEqualsSignindex + 1);

            return HttpUtility.UrlEncode(signedInput.Substring(0, 43));
        }

        private Boolean getResponse(HttpWebRequest request)
        {
            try
            {
                response = request.GetResponse() as HttpWebResponse;
                return true;
            }
            catch (WebException e)
            {
                Console.WriteLine("Exception Message :" + e.Message);
                if (e.Status == WebExceptionStatus.ProtocolError)
                {
                    var response = ((HttpWebResponse)e.Response);
                    Console.WriteLine("Status Code : {0}", ((HttpWebResponse)e.Response).StatusCode);
                    Console.WriteLine("Status Description : {0}", ((HttpWebResponse)e.Response).StatusDescription);

                    var stream = response.GetResponseStream();
                    var reader = new StreamReader(stream);
                    var text = reader.ReadToEnd();
                    Console.WriteLine("Response Description : {0}", text);

                }
                return false;
            }
        }
    }
}

