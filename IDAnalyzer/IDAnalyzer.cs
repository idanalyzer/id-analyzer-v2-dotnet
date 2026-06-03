using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;

namespace IDAnalyzer
{
    /// <summary>
    /// Internal helper utilities shared by the SDK: input image parsing, API endpoint resolution and API error handling.
    /// </summary>
    public class Common
    {
        /// <summary>
        /// Normalize an input image into a value accepted by the API. URLs and (when allowed) cache references are passed through unchanged, local file paths are read and base64-encoded, and long strings are assumed to already be base64 content.
        /// </summary>
        /// <param name="str">Input image as a file path, http(s) URL, base64 content, or (when <paramref name="allowCache"/> is true) a "ref:" cache reference.</param>
        /// <param name="allowCache">When true, a value beginning with "ref:" is treated as a cache reference and returned unchanged.</param>
        /// <returns>A URL, cache reference, or base64-encoded image string suitable for use in an API request payload.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the input is not a valid URL, the file does not exist, and the string is too short to be image content.</exception>
        public static string ParseInput(string str, bool allowCache = false)
        {
            if (allowCache && str.Substring(0, 4) == "ref:")
            {
                return str;
            }

            try
            {
                Uri uri = new Uri(str);
                if (uri.Scheme.ToLower() == "http" || uri.Scheme.ToLower() == "https")
                {
                    return str;
                }
            }
            catch (UriFormatException e) { }
            if (File.Exists(str))
            {
                var data = File.ReadAllBytes(str);
                return Convert.ToBase64String(data);
            }

            if (str.Length > 100)
            {
                return str;
            }

            throw new InvalidArgumentException("Invalid input image, file not found or malformed URL.");
        }

        /// <summary>
        /// Build a full API endpoint URL from a relative path (or pass an absolute URL through unchanged), resolving the API region base URL from the IDANALYZER_REGION environment variable and appending any query parameters.
        /// </summary>
        /// <param name="uri">A relative API path (e.g. "scan") or an absolute http(s) URL which is returned unchanged.</param>
        /// <param name="parameters">Optional query string parameters appended to the URL as key=value pairs.</param>
        /// <returns>The fully-qualified endpoint URL.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when IDANALYZER_REGION is set to a value other than "us" or "eu".</exception>
        public static string GetEndpoint(string uri, Hashtable? parameters = null)
        {
            string paramStr = "";
            if (parameters != null)
            {
                List<string> paramStrs = new List<string> { };
                foreach (DictionaryEntry field in parameters)
                {
                    paramStrs.Add(String.Format("{0}={1}", field.Key, field.Value));
                }
                paramStr = String.Join("&", paramStrs);
            }
            if (uri.Substring(0, 4).ToLower() == "http")
            {
                return uri;
            }

            var region = Environment.GetEnvironmentVariable("IDANALYZER_REGION");
            if (region == null || region == "")
            {
                region = "us";
            }
            region = region.ToLower();

            string baseUrl;
            if (region == "us")
            {
                baseUrl = "https://api2.idanalyzer.com";
            }
            else if (region == "eu")
            {
                baseUrl = "https://api2-eu.idanalyzer.com";
            }
            else
            {
                throw new InvalidArgumentException(
                    String.Format("Invalid IDANALYZER_REGION '{0}', valid regions are: us, eu.", region));
            }

            return String.Format("{0}/{1}{2}", baseUrl, uri, paramStr != "" ? "?" + paramStr : "");
        }

        /// <summary>
        /// Parse an API HTTP response body into a JSON object and, optionally, raise an exception when the response contains an API error.
        /// </summary>
        /// <param name="resp">The HTTP response returned by the API.</param>
        /// <param name="throwError">When true, an <see cref="ApiError"/> is thrown if the response body contains an "error" object.</param>
        /// <returns>The parsed API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="ApiError">Thrown when <paramref name="throwError"/> is true and the API response contains an error.</exception>
        public static JObject ApiExceptionHandle(HttpResponseMessage resp, bool throwError)
        {
            var result = JObject.Parse(resp.Content.ReadAsStringAsync().Result);
            if (throwError && result.ContainsKey("error"))
            {
                throw new ApiError(result["error"]["message"].ToString(), result["error"]["code"].ToString());
            }
            return result;
        }
    }

    /// <summary>
    /// Base class for all API clients. Handles API key resolution, the shared HTTP session, request configuration and the throw-on-error toggle.
    /// </summary>
    public class ApiParent
    {
        /// <summary>The resolved API key used to authenticate requests.</summary>
        protected string? apiKey = null;

        /// <summary>Client library identifier sent with every request.</summary>
        protected string client_lib = "csharp-sdk";

        /// <summary>Accumulated request configuration / payload parameters.</summary>
        protected Hashtable config = new Hashtable();

        /// <summary>Whether an <see cref="ApiError"/> is thrown when the API returns an error.</summary>
        protected bool throwError = false;

        /// <summary>The shared HTTP client used to send API requests.</summary>
        protected HttpClient sess = new HttpClient();

        /// <summary>
        /// Initialize the API client, resolving the API key from the provided value or the IDANALYZER_KEY environment variable and configuring the HTTP session.
        /// </summary>
        /// <param name="apiKey">Your API key. If null, the IDANALYZER_KEY environment variable is used.</param>
        /// <exception cref="Exception">Thrown when no API key can be resolved from the argument or the IDANALYZER_KEY environment variable.</exception>
        public ApiParent(string? apiKey = null)
        {
            this.apiKey = this.getApiKey(apiKey);
            if (this.apiKey == null || this.apiKey! == "")
            {
                throw new Exception("Please set API key via environment variable 'IDANALYZER_KEY'");
            }

            this.config["client"] = this.client_lib;
            this.sess.DefaultRequestHeaders.Add("X-Api-Key", this.apiKey);
        }

        /// <summary>
        /// Resolve the API key, preferring the provided value and falling back to the IDANALYZER_KEY environment variable.
        /// </summary>
        /// <param name="apiKey">An explicit API key, or null to read from the environment.</param>
        /// <returns>The resolved API key, or null if none is available.</returns>
        protected string? getApiKey(string? apiKey = null)
        {
            return apiKey ?? Environment.GetEnvironmentVariable("IDANALYZER_KEY");
        }

        /// <summary>
        /// Set an API parameter and its value, this function allows you to set any API parameter without using the built-in functions
        /// </summary>
        /// <param name="key">Parameter key</param>
        /// <param name="value">Parameter value</param>
        public void setParam(string key, object? value)
        {
            this.config.Add(key, value);
        }

        /// <summary>
        /// Whether an exception should be thrown if API response contains an error message
        /// </summary>
        /// <param name="sw">Throw exception upon API error, defaults to false</param>
        public void throwApiException(bool sw = false)
        {
            this.throwError = sw;
        }
    }

    /// <summary>
    /// Represents a KYC validation profile. Use a preset (security_none/low/medium/high) or a custom profile ID, and optionally override individual settings before passing it to a scan, biometric or Docupass call.
    /// </summary>
    public class Profile
    {
        /// <summary>Preset profile ID that applies no validation security.</summary>
        public static string SECURITY_NONE = "security_none";

        /// <summary>Preset profile ID that applies low validation security.</summary>
        public static string SECURITY_LOW = "security_low";

        /// <summary>Preset profile ID that applies medium validation security.</summary>
        public static string SECURITY_MEDIUM = "security_medium";

        /// <summary>Preset profile ID that applies high validation security.</summary>
        public static string SECURITY_HIGH = "security_high";

        private string URL_VALIDATION_REGEX =
            "((?:[a-z][\\w-]+:(?:/{1,3}|[a-z0-9%])|www\\d{0,3}[.]|[a-z0-9.\\-]+[.][a-z]{2,4}/)(?:[^\\s()<>]+|\\(([^\\s()<>]+|(\\([^\\s()<>]+\\)))*\\))+(?:\\(([^\\s()<>]+|(\\([^\\s()<>]+\\)))*\\)|[^\\s`!()\\[\\]{};:'\".,<>?«»“”‘’]))";

        /// <summary>The profile ID (custom ID or one of the preset security levels) sent to the API.</summary>
        public string profileId = "";

        /// <summary>Per-call override settings layered on top of the base profile.</summary>
        public Hashtable profileOverride = new Hashtable();

        /// <summary>
        /// Initialize KYC Profile
        /// </summary>
        /// <param name="profileId">Custom profile ID or preset profile (security_none, security_low, security_medium, security_high). SECURITY_NONE will be used if left blank.</param>
        public Profile(string profileId = "")
        {
            this.profileId = profileId != "" ? profileId : SECURITY_NONE;
        }

        /// <summary>
        /// Set profile configuration with provided JSON string
        /// </summary>
        /// <param name="jsonStr">JSON string containing profile information.</param>
        public void loadFromJson(string jsonStr)
        {
            var res = JsonConvert.DeserializeObject<Hashtable>(jsonStr);
            if (res == null) return;
            this.profileOverride = res;
        }

        /// <summary>
        /// Canvas Size in pixels, input image larger than this size will be scaled down before further processing, reduced image size will improve inference time but reduce result accuracy. Set 0 to disable image resizing.
        /// </summary>
        /// <param name="pixels">Maximum canvas size in pixels; set 0 to disable image resizing.</param>
        public void canvasSize(int pixels)
        {
            this.profileOverride["canvasSize"] = pixels;
        }

        /// <summary>
        /// Correct image orientation for rotated images
        /// </summary>
        /// <param name="enabled">True to enable automatic orientation correction.</param>
        public void orientationCorrection(bool enabled)
        {
            this.profileOverride["orientationCorrection"] = enabled;
        }

        /// <summary>
        /// Enable to automatically detect and return the locations of signature, document and face.
        /// </summary>
        /// <param name="enabled">True to enable object detection.</param>
        public void objectDetection(bool enabled)
        {
            this.profileOverride["objectDetection"] = enabled;
        }

        /// <summary>
        /// Enable to parse AAMVA barcode for US/CA ID/DL. Disable this to improve performance if you are not planning on scanning ID/DL from US or Canada.
        /// </summary>
        /// <param name="enabled">True to enable AAMVA barcode parsing.</param>
        public void AAMVABarcodeParsing(bool enabled)
        {
            this.profileOverride["AAMVABarcodeParsing"] = enabled;
        }

        /// <summary>
        /// Whether scan transaction results and output images should be saved on cloud
        /// </summary>
        /// <param name="enableSaveTransaction">True to save the transaction result on the cloud.</param>
        /// <param name="enableSaveTransactionImages">True to also save the transaction output images on the cloud.</param>
        public void saveResult(bool enableSaveTransaction, bool enableSaveTransactionImages)
        {
            this.profileOverride["saveResult"] = enableSaveTransaction;
            if (enableSaveTransactionImages)
            {
                this.profileOverride["saveImage"] = enableSaveTransactionImages;
            }
        }

        /// <summary>
        /// Whether to return output image as part of API response
        /// </summary>
        /// <param name="enableOutputImage">True to return the output image in the API response.</param>
        /// <param name="outputFormat">Output image format, "url" or "base64".</param>
        public void outputImage(bool enableOutputImage, string outputFormat = "url")
        {
            this.profileOverride["outputImage"] = enableOutputImage;
            if (enableOutputImage)
            {
                this.profileOverride["outputType"] = outputFormat;
            }
        }

        /// <summary>
        /// Crop image before saving and returning output
        /// </summary>
        /// <param name="enableAutoCrop">True to automatically crop the document from the image.</param>
        /// <param name="enableAdvancedAutoCrop">True to enable advanced auto-cropping.</param>
        public void autoCrop(bool enableAutoCrop, bool enableAdvancedAutoCrop)
        {
            this.profileOverride["crop"] = enableAutoCrop;
            this.profileOverride["advancedCrop"] = enableAdvancedAutoCrop;
        }

        /// <summary>
        /// Maximum width/height in pixels for output and saved image.
        /// </summary>
        /// <param name="pixels">Maximum output image width/height in pixels.</param>
        public void outputSize(int pixels)
        {
            this.profileOverride["outputSize"] = pixels;
        }

        /// <summary>
        /// Generate a full name field using parsed first name, middle name and last name.
        /// </summary>
        /// <param name="enabled">True to infer and populate the full name field.</param>
        public void inferFullName(bool enabled)
        {
            this.profileOverride["inferFullName"] = enabled;
        }

        /// <summary>
        /// If first name contains more than one word, move second word onwards into middle name field.
        /// </summary>
        /// <param name="enabled">True to split a multi-word first name into first and middle name fields.</param>
        public void splitFirstName(bool enabled)
        {
            this.profileOverride["splitFirstName"] = enabled;
        }

        /// <summary>
        /// Enable to generate a detailed PDF audit report for every transaction.
        /// </summary>
        /// <param name="enabled">True to generate a PDF audit report for each transaction.</param>
        public void transactionAuditReport(bool enabled)
        {
            this.profileOverride["transactionAuditReport"] = enabled;
        }

        /// <summary>
        /// Set timezone for audit reports. If left blank, UTC will be used. Refer to https://en.wikipedia.org/wiki/List_of_tz_database_time_zones TZ database name list.
        /// </summary>
        /// <param name="timezone">TZ database timezone name (e.g. "America/New_York"); blank uses UTC.</param>
        public void setTimezone(string timezone)
        {
            this.profileOverride["timezone"] = timezone;
        }

        /// <summary>
        /// A list of data field keys to be redacted before transaction storage, these fields will also be blurred from output and saved image.
        /// </summary>
        /// <param name="fieldKeys">Array of data field keys to redact and blur.</param>
        public void obscure(string[] fieldKeys)
        {
            this.profileOverride["obscure"] = fieldKeys;
        }

        /// <summary>
        /// Enter a server URL to listen for Docupass verification and scan transaction results
        /// </summary>
        /// <param name="url">Publicly reachable http(s) webhook URL to receive transaction results.</param>
        /// <exception cref="InvalidArgumentException">Thrown when the URL is malformed, points to localhost, or uses a protocol other than http/https.</exception>
        public void webhook(string url = "https://www.example.com/webhook.php")
        {
            Uri uri;
            try
            {
                uri = new Uri(url);
            }
            catch (UriFormatException e)
            {
                throw new InvalidArgumentException("Invalid URL format");
            }

            IPAddress? ipinfo = null;
            try
            {
                ipinfo = IPAddress.Parse(uri.Host);
                if (ipinfo.ToString() == "localhost")
                {
                    throw new InvalidArgumentException("Invalid URL, the host does not appear to be a remote host.");
                }
            }
            catch (FormatException e)
            {
            }

            if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                throw new InvalidArgumentException("Invalid URL, only http and https protocols are allowed.");
            }

            this.profileOverride["webhook"] = url;
        }

        /// <summary>
        /// Set validation threshold of a specified component
        /// </summary>
        /// <param name="thresholdKey">The threshold component key to set.</param>
        /// <param name="thresholdValue">The threshold value to apply to the component.</param>
        public void threshold(string thresholdKey, double thresholdValue)
        {
            if (!this.profileOverride.Contains("thresholds"))
            {
                this.profileOverride["thresholds"] = new Hashtable();
            }

            ((Hashtable)this.profileOverride["thresholds"])[thresholdKey] = thresholdValue;
        }

        /// <summary>
        ///  Set decision trigger value
        /// </summary>
        /// <param name="reviewTrigger">If the final total review score is equal to or greater than this value, the final KYC decision will be "review"</param>
        /// <param name="rejectTrigger">If the final total review score is equal to or greater than this value, the final KYC decision will be "reject". Reject has higher priority than review.</param>
        public void decisionTrigger(double reviewTrigger = 1, double rejectTrigger = 1)
        {
            this.profileOverride["decisionTrigger"] = new Hashtable
        {
            { "review", reviewTrigger },
            { "reject", rejectTrigger },
        };
        }

        /// <summary>
        /// Enable/Disable and fine-tune how each Document Validation Component affects the final decision.
        /// </summary>
        /// <param name="code">Document Validation Component Code / Warning Code</param>
        /// <param name="enabled">Enable the current Document Validation Component</param>
        /// <param name="reviewThreshold">If the current validation has failed to pass, and the specified number is greater than or equal to zero, and the confidence of this warning is greater than or equal to the specified value, the "total review score" will be added by the weight value.</param>
        /// <param name="rejectThreshold">If the current validation has failed to pass, and the specified number is greater than or equal to zero, and the confidence of this warning is greater than or equal to the specified value, the "total reject score" will be added by the weight value.</param>
        /// <param name="weight">Weight to add to the total review and reject score if the validation has failed to pass.</param>
        public void setWarning(string code = "UNRECOGNIZED_DOCUMENT", bool enabled = true, double reviewThreshold = -1,
            double rejectThreshold = 0, double weight = 1)
        {
            if (!this.profileOverride.Contains("decisions"))
            {
                this.profileOverride["decisions"] = new Hashtable();
            }

            ((Hashtable)this.profileOverride["decisions"])[code] = new Hashtable
            {
            { "enabled", enabled },
            { "review", reviewThreshold },
            { "reject", rejectThreshold },
            { "weight", weight },
            };
        }

        /// <summary>
        /// Check if the document was issued by specified countries. Separate multiple values with comma. For example "US,CA" would accept documents from the United States and Canada.
        /// </summary>
        /// <param name="countryCodes">ISO ALPHA-2 Country Code separated by comma</param>
        public void restrictDocumentCountry(string countryCodes = "US,CA,UK")
        {
            if (!this.profileOverride.Contains("acceptedDocuments"))
            {
                this.profileOverride["acceptedDocuments"] = new Hashtable();
            }

            ((Hashtable)this.profileOverride["acceptedDocuments"])["documentCountry"] = countryCodes;
        }

        /// <summary>
        /// Check if the document was issued by specified state. Separate multiple values with comma. For example "CA,TX" would accept documents from California and Texas.
        /// </summary>
        /// <param name="states">State full name or abbreviation separated by comma</param>
        public void restrictDocumentState(string states = "CA,TX")
        {
            if (!this.profileOverride.Contains("acceptedDocuments"))
            {
                this.profileOverride["acceptedDocuments"] = new Hashtable();
            }

            ((Hashtable)this.profileOverride["acceptedDocuments"])["documentState"] = states;
        }

        /// <summary>
        /// Check if the document was one of the specified types. For example, "PD" would accept both passport and driver license.
        /// </summary>
        /// <param name="documentType">P: Passport, D: Driver's License, I: Identity Card</param>
        public void restrictDocumentType(string documentType = "DIP")
        {
            if (!this.profileOverride.Contains("acceptedDocuments"))
            {
                this.profileOverride["acceptedDocuments"] = new Hashtable();
            }

            ((Hashtable)this.profileOverride["acceptedDocuments"])["documentType"] = documentType;
        }
    }

    /// <summary>
    /// Client for biometric verification: 1:1 face verification and standalone liveness checks.
    /// </summary>
    public class Biometric : ApiParent
    {
        /// <summary>
        /// Initialize the Biometric client.
        /// </summary>
        /// <param name="apiKey">Your API key. If null, the IDANALYZER_KEY environment variable is used.</param>
        public Biometric(string? apiKey = null) : base(apiKey)
        {
            this.config["profile"] = "";
            this.config["profileOverride"] = new Hashtable();
            this.config["customData"] = "";
        }

        /// <summary>
        /// Set an arbitrary string you wish to save with the transaction. e.g Internal customer reference number
        /// </summary>
        /// <param name="customData">Arbitrary string to store with the transaction.</param>
        public void setCustomData(string customData)
        {
            this.config["customData"] = customData;
        }

        /// <summary>
        /// Set KYC Profile
        /// </summary>
        /// <param name="profile">KYCProfile object</param>
        /// <exception cref="InvalidArgumentException">Thrown when the provided object is not a <see cref="Profile"/>.</exception>
        public void setProfile(object? profile)
        {
            if (profile is Profile)
            {
                var _profile = ((Profile)profile);
                this.config["profile"] = _profile.profileId;
                if (_profile.profileOverride.Count > 0)
                {
                    this.config["profileOverride"] = _profile.profileOverride;
                }
                else
                {
                    this.config.Remove("profileOverride");
                }
            }
            else
            {
                throw new InvalidArgumentException("Provided profile is not a 'KYCProfile' object.");
            }
        }

        /// <summary>
        /// Perform 1:1 face verification using selfie photo or selfie video, against a reference face image.
        /// </summary>
        /// <param name="referenceFaceImage">Front of Document (file path, base64 content, url, or cache reference)</param>
        /// <param name="facePhoto">Face Photo (file path, base64 content or URL, or cache reference)</param>
        /// <param name="faceVideo">Face Video (file path, base64 content or URL)</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when no KYC profile is set, the reference image is missing, or neither a face photo nor face video is provided.</exception>
        public JObject verifyFace(string referenceFaceImage, string facePhoto, string faceVideo = "")
        {
            if ((string)this.config["profile"] == "")
                throw new InvalidArgumentException(
                    "KYC Profile not configured, please use setProfile before calling this function.");
            var payload = this.config;
            if (referenceFaceImage == "")
            {
                throw new InvalidArgumentException("Reference face image required.");
            }

            if (facePhoto == "" && faceVideo == "")
            {
                throw new InvalidArgumentException("Verification face image required.");
            }

            payload["reference"] = Common.ParseInput(referenceFaceImage, true);
            if (facePhoto != "")
            {
                payload["face"] = Common.ParseInput(facePhoto, true);
            }
            else if (faceVideo != "")
            {
                payload["faceVideo"] = Common.ParseInput(faceVideo);
            }

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var resp = this.sess.PostAsync(Common.GetEndpoint("face"), content).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>
        /// Perform standalone liveness check on a selfie photo or video.
        /// </summary>
        /// <param name="facePhoto">Face Photo (file path, base64 content or URL, or cache reference)</param>
        /// <param name="faceVideo">Face Video (file path, base64 content or URL)</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when no KYC profile is set, or neither a face photo nor face video is provided.</exception>
        public JObject verifyLiveness(string facePhoto = "", string faceVideo = "")
        {
            if ((string)this.config["profile"] == "")
            {
                throw new InvalidArgumentException(
                    "KYC Profile not configured, please use setProfile before calling this function.");
            }

            var payload = this.config;
            if (facePhoto == "" && faceVideo == "")
            {
                throw new InvalidArgumentException("Verification face image required.");
            }

            if (facePhoto != "")
            {
                payload["face"] = Common.ParseInput(facePhoto, true);
            }
            else if (faceVideo != "")
            {
                payload["faceVideo"] = Common.ParseInput(faceVideo);
            }

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var resp = this.sess.PostAsync(Common.GetEndpoint("liveness"), content).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }
    }

    /// <summary>
    /// Client for managing contract templates and generating documents from them.
    /// </summary>
    public class Contract : ApiParent
    {
        /// <summary>
        /// Initialize the Contract client.
        /// </summary>
        /// <param name="apiKey">Your API key. If null, the IDANALYZER_KEY environment variable is used.</param>
        public Contract(string? apiKey = null) : base(apiKey)
        {
        }

        /// <summary>
        /// Generate document using template and transaction data
        /// </summary>
        /// <param name="templateId">Template ID</param>
        /// <param name="format">PDF, DOCX or HTML</param>
        /// <param name="transactionId">Fill the template with data from specified transaction</param>
        /// <param name="fillData">Array data in key-value pairs to autofill dynamic fields, data from user ID will be used first in case of a conflict. For example, passing {"myparameter":"abc"} would fill %{myparameter} in contract template with "abc".</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the template ID is empty.</exception>
        public JObject generate(string templateId, string format = "PDF", string transactionId = "",
            Hashtable? fillData = null)
        {
            if (fillData == null)
            {
                fillData = new Hashtable() { };
            }

            var payload = new Hashtable()
        {
            { "format", format },
        };
            if (templateId == "")
            {
                throw new InvalidArgumentException("Template ID required.");
            }

            payload["templateId"] = templateId;
            if (transactionId != "")
            {
                payload["transactionId"] = transactionId;
            }

            if (fillData.Count > 0)
            {
                payload["fillData"] = fillData;
            }

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var resp = this.sess.PostAsync(Common.GetEndpoint("generate"), content).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>
        /// Retrieve a list of contract templates
        /// </summary>
        /// <param name="order">Sort results by newest(-1) or oldest(1)</param>
        /// <param name="limit">Number of items to be returned per call</param>
        /// <param name="offset">Start the list from a particular entry index</param>
        /// <param name="filterTemplateId">Filter result by template ID</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when 'order' is not 1 or -1, or 'limit' is outside the range 1-100.</exception>
        public JObject listTemplate(int order = -1, int limit = 10, int offset = 0, string filterTemplateId = "")
        {
            if (order != 1 && order != -1)
            {
                throw new InvalidArgumentException("\'order\' should be integer of 1 or -1.");
            }

            if (limit <= 0 || limit > 100)
            {
                throw new InvalidArgumentException(
                    "'limit' should be a positive integer greater than 0 and less than or equal to 100.");
            }

            var payload = new Hashtable()
        {
            { "order", order },
            { "limit", limit },
            { "offset", offset },
        };

            if (filterTemplateId != "")
            {
                payload["templateId"] = filterTemplateId;
            }

            var resp = this.sess.GetAsync(Common.GetEndpoint("contract", payload)).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>
        /// Get contract template
        /// </summary>
        /// <param name="templateId">Template ID</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the template ID is empty.</exception>
        public JObject getTemplate(string templateId = "")
        {
            if (templateId == "")
            {
                throw new InvalidArgumentException("Template ID required.");
            }

            var resp = this.sess.GetAsync(Common.GetEndpoint(String.Format("contract/{0}", templateId))).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>
        /// Delete contract template
        /// </summary>
        /// <param name="templateId">Template ID</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the template ID is empty.</exception>
        public JObject deleteTemplate(string templateId = "")
        {
            if (templateId == "")
            {
                throw new InvalidArgumentException("Template ID required.");
            }

            var resp = this.sess.DeleteAsync(Common.GetEndpoint(String.Format("contract/{0}", templateId))).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>
        /// Create new contract template
        /// </summary>
        /// <param name="name">Template name</param>
        /// <param name="_content">Template HTML content</param>
        /// <param name="orientation">0=Portrait(Default) 1=Landscape</param>
        /// <param name="timezone">Template timezone</param>
        /// <param name="font">Template font</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the template name or content is empty.</exception>
        public JObject createTemplate(string name = "", string _content = "", string orientation = "",
            string timezone = "UTC", string font = "Open Sans")
        {
            if (name == "")
            {
                throw new InvalidArgumentException("Template name required.");
            }

            if (_content == "")
            {
                throw new InvalidArgumentException("Template content required.");
            }

            var payload = new Hashtable()
        {
            { "name", name },
            { "content", _content },
            { "orientation", orientation },
            { "timezone", timezone },
            { "font", font },
        };
            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var resp = this.sess.PostAsync(Common.GetEndpoint("contract"), content).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>
        /// Update contract template
        /// </summary>
        /// <param name="templateId">Template ID</param>
        /// <param name="name">Template name</param>
        /// <param name="_content">Template HTML content</param>
        /// <param name="orientation">0=Portrait(Default) 1=Landscape</param>
        /// <param name="timezone">Template timezone</param>
        /// <param name="font">Template font</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the template ID, name or content is empty.</exception>
        public JObject updateTemplate(string templateId = "", string name = "", string _content = "",
            string orientation = "0", string timezone = "UTC", string font = "Open Sans")
        {
            if (templateId == "")
            {
                throw new InvalidArgumentException("Template ID required.");
            }

            if (name == "")
            {
                throw new InvalidArgumentException("Template name required.");
            }

            if (_content == "")
            {
                throw new InvalidArgumentException("Template content required.");
            }

            var payload = new Hashtable()
        {
            { "name", name },
            { "content", _content },
            { "orientation", orientation },
            { "timezone", timezone },
            { "font", font },
        };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var resp = this.sess.PostAsync(Common.GetEndpoint(String.Format("contract/{0}", templateId)), content).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }
    }

    /// <summary>
    /// Client for scanning identity documents (full scan, quick scan and very-quick OCR scan) and configuring related verification options.
    /// </summary>
    public class Scanner : ApiParent
    {
        /// <summary>
        /// Initialize the Scanner client.
        /// </summary>
        /// <param name="apiKey">Your API key. If null, the IDANALYZER_KEY environment variable is used.</param>
        public Scanner(string? apiKey = null) : base(apiKey)
        {
            this.config["document"] = "";
            this.config["documentBack"] = "";
            this.config["face"] = "";
            this.config["faceVideo"] = "";
            this.config["profile"] = "";
            this.config["profileOverride"] = new Hashtable() { };
            this.config["verifyName"] = "";
            this.config["verifyDob"] = "";
            this.config["verifyAge"] = "";
            this.config["verifyAddress"] = "";
            this.config["verifyPostcode"] = "";
            this.config["verifyDocumentNumber"] = "";
            this.config["restrictCountry"] = "";
            this.config["restrictState"] = "";
            this.config["restrictType"] = "";
            this.config["ip"] = "";
            this.config["customData"] = "";
        }

        /// <summary>
        /// Pass in user IP address to check if ID is issued from the same country as the IP address, if no value is provided http connection IP will be used.
        /// </summary>
        /// <param name="ip">The user's IP address.</param>
        public void setUserIp(string ip)
        {
            this.config["ip"] = ip;
        }

        /// <summary>
        /// Set an arbitrary string you wish to save with the transaction. e.g Internal customer reference number
        /// </summary>
        /// <param name="customData">Arbitrary string to store with the transaction.</param>
        public void setCustomData(string customData)
        {
            this.config["customData"] = customData;
        }

        /// <summary>
        /// Automatically generate contract document using value parsed from uploaded ID
        /// </summary>
        /// <param name="templateId">Enter up to 5 contract template ID (seperated by comma)</param>
        /// <param name="_format">PDF, DOCX or HTML</param>
        /// <param name="extraFillData">Array data in key-value pairs to autofill dynamic fields, data from user ID will be used first in case of a conflict. For example, passing {"myparameter":"abc"} would fill %{myparameter} in contract template with "abc".</param>
        public void setContractOptions(string templateId = "", string _format = "PDF", Hashtable? extraFillData = null)
        {
            if (extraFillData == null)
            {
                extraFillData = new Hashtable() { };
            }

            if (templateId != "")
            {
                this.config["contractGenerate"] = templateId;
                this.config["contractFormat"] = _format;
                if (extraFillData.Count > 0)
                {
                    this.config["contractPrefill"] = extraFillData;
                }
                else
                {
                    this.config.Remove("contractPrefill");
                }
            }
            else
            {
                this.config.Remove("contractGenerate");
                this.config.Remove("contractFormat");
                this.config.Remove("contractPrefill");
            }
        }

        /// <summary>
        /// Set KYC Profile
        /// </summary>
        /// <param name="profile">KYCProfile object</param>
        /// <exception cref="InvalidArgumentException">Thrown when the provided object is not a <see cref="Profile"/>.</exception>
        public void setProfile(object? profile)
        {
            if (profile is Profile)
            {
                var p = (Profile)profile;
                this.config["profile"] = p.profileId;
                if (p.profileOverride.Count > 0)
                {
                    this.config["profileOverride"] = p.profileOverride;
                }
                else
                {
                    this.config.Remove("profileOverride");
                }
            }
            else
            {
                throw new InvalidArgumentException("Provided profile is not a 'KYCProfile' object.");
            }
        }

        /// <summary>
        /// Check if customer information matches with uploaded document
        /// </summary>
        /// <param name="documentNumber">Document or ID number</param>
        /// <param name="fullName">Full name</param>
        /// <param name="dob">Date of birth in YYYY/MM/DD</param>
        /// <param name="ageRange">Age range, example: 18-40</param>
        /// <param name="address">Address</param>
        /// <param name="postcode">Postcode</param>
        /// <exception cref="InvalidArgumentException">Thrown when the date of birth is not in YYYY/MM/DD format or the age range is not in minAge-maxAge format.</exception>
        public void verifyUserInformation(string documentNumber = "", string fullName = "", string dob = "",
            string ageRange = "", string address = "", string postcode = "")
        {
            this.config["verifyDocumentNumber"] = documentNumber;
            this.config["verifyName"] = fullName;
            if (dob == "")
            {
                this.config["verifyDob"] = dob;
            }
            else
            {
                DateTime result;
                if (DateTime.TryParseExact(dob, "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                        out result))
                {
                    this.config["verifyDob"] = dob;
                }
                else
                {
                    throw new InvalidArgumentException("Invalid birthday format (YYYY/MM/DD)");
                }
            }

            if (ageRange == "")
            {
                this.config["verifyAge"] = ageRange;
            }
            else
            {
                if (!Regex.IsMatch(ageRange, "^\\d+-\\d+$"))
                {
                    throw new InvalidArgumentException("Invalid age range format (minAge-maxAge)");
                }

                this.config["verifyAge"] = ageRange;
            }

            this.config["verifyAddress"] = address;
            this.config["verifyPostcode"] = postcode;
        }

        /// <summary>
        /// Check if the document was issued by specified countries. Separate multiple values with comma. For example "US,CA" would accept documents from the United States and Canada.
        /// </summary>
        /// <param name="countryCodes">ISO ALPHA-2 Country Code separated by comma</param>
        public void restrictCountry(string countryCodes = "US,CA,UK")
        {
            this.config["restrictCountry"] = countryCodes;
        }

        /// <summary>
        /// Check if the document was issued by specified state. Separate multiple values with comma. For example "CA,TX" would accept documents from California and Texas.
        /// </summary>
        /// <param name="states">State full name or abbreviation separated by comma</param>
        public void restrictState(string states = "CA,TX")
        {
            this.config["restrictState"] = states;
        }

        /// <summary>
        /// Check if the document was one of the specified types. For example, "PD" would accept both passport and driver license.
        /// </summary>
        /// <param name="documentType">P: Passport, D: Driver's License, I: Identity Card</param>
        public void restrictType(string documentType = "DIP")
        {
            this.config["restrictType"] = documentType;
        }

        /// <summary>
        /// Initiate a new identity document scan and ID face verification transaction by providing input images.
        /// </summary>
        /// <param name="documentFront">Front of Document (file path, base64 content, url, or cache reference)</param>
        /// <param name="documentBack">Back of Document (file path, base64 content or URL, or cache reference)</param>
        /// <param name="facePhoto">Face Photo (file path, base64 content or URL, or cache reference)</param>
        /// <param name="faceVideo">Face Video (file path, base64 content or URL)</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when no KYC profile is set or the front document image is missing.</exception>
        public JObject scan(string documentFront = "", string documentBack = "", string facePhoto = "",
            string faceVideo = "")
        {
            if ((string)this.config["profile"] == "")
            {
                throw new InvalidArgumentException(
                    "KYC Profile not configured, please use setProfile before calling this function.");
            }

            var payload = this.config;
            if (documentFront == "")
            {
                throw new InvalidArgumentException("Primary document image required.");
            }

            payload["document"] = Common.ParseInput(documentFront, true);
            if (documentBack != "")
            {
                payload["documentBack"] = Common.ParseInput(documentBack, true);
            }

            if (facePhoto != "")
            {
                payload["face"] = Common.ParseInput(facePhoto, true);
            }
            else if (faceVideo != "")
            {
                payload["faceVideo"] = Common.ParseInput(faceVideo);
            }

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var resp = this.sess.PostAsync(Common.GetEndpoint("scan"), content).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>
        /// Initiate a quick identity document OCR scan by providing input images.
        /// </summary>
        /// <param name="documentFront">Front of Document (file path, base64 content or URL)</param>
        /// <param name="documentBack">Back of Document (file path, base64 content or URL)</param>
        /// <param name="cacheImage">Cache uploaded image(s) for 24 hours and obtain a cache reference for each image, the reference hash can be used to start standard scan transaction without re-uploading the file.</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the front document image is missing.</exception>
        public JObject quickScan(string documentFront = "", string documentBack = "", bool cacheImage = false)
        {
            var payload = new Hashtable()
        {
            { "saveFile", cacheImage },
        };

            if (documentFront == "")
            {
                throw new InvalidArgumentException("Primary document image required.");
            }

            payload["document"] = Common.ParseInput(documentFront, true);
            if (documentBack != "")
            {
                payload["documentBack"] = Common.ParseInput(documentBack, true);
            }

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var resp = this.sess.PostAsync(Common.GetEndpoint("quickscan"), content).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>
        /// Initiate a very quick (fast) identity document OCR scan by providing input images. Faster but less thorough than quickScan, useful for high-throughput OCR-only use cases.
        /// </summary>
        /// <param name="documentFront">Front of Document (file path, base64 content or URL)</param>
        /// <param name="documentBack">Back of Document (file path, base64 content or URL)</param>
        /// <param name="cacheImage">Cache uploaded image(s) for 24 hours and obtain a cache reference for each image, the reference hash can be used to start standard scan transaction without re-uploading the file.</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the front document image is missing.</exception>
        public JObject veryQuickScan(string documentFront = "", string documentBack = "", bool cacheImage = false)
        {
            var payload = new Hashtable()
        {
            { "saveFile", cacheImage },
        };

            if (documentFront == "")
            {
                throw new InvalidArgumentException("Primary document image required.");
            }

            payload["document"] = Common.ParseInput(documentFront);
            if (documentBack != "")
            {
                payload["documentBack"] = Common.ParseInput(documentBack);
            }

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var resp = this.sess.PostAsync(Common.GetEndpoint("veryquickscan"), content).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }
    }

    /// <summary>
    /// Client for retrieving, listing, updating, deleting and exporting transaction records, and downloading transaction images and files.
    /// </summary>
    public class Transaction : ApiParent
    {
        /// <summary>
        /// Initialize the Transaction client.
        /// </summary>
        /// <param name="apiKey">Your API key. If null, the IDANALYZER_KEY environment variable is used.</param>
        public Transaction(string? apiKey = null) : base(apiKey)
        {
        }

        /// <summary>
        /// Retrieve a single transaction record
        /// </summary>
        /// <param name="transactionId">Transaction ID</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the transaction ID is empty.</exception>
        public JObject getTransaction(string transactionId = "")
        {
            if (transactionId == "")
            {
                throw new InvalidArgumentException("Transaction ID required.");
            }

            var resp = this.sess.GetAsync(Common.GetEndpoint(String.Format("transaction/{0}", transactionId))).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>
        /// Retrieve a list of transaction history
        /// </summary>
        /// <param name="order">Sort results by newest(-1) or oldest(1)</param>
        /// <param name="limit">Number of items to be returned per call</param>
        /// <param name="offset">Start the list from a particular entry index</param>
        /// <param name="createdAtMin">List transactions that were created after this timestamp</param>
        /// <param name="createdAtMax">List transactions that were created before this timestamp</param>
        /// <param name="filterCustomData">Filter result by customData field</param>
        /// <param name="filterDecision">Filter result by decision (accept, review, reject)</param>
        /// <param name="filterDocupass">Filter result by Docupass reference</param>
        /// <param name="filterProfileId">Filter result by KYC Profile ID</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when 'order' is not 1 or -1, or 'limit' is outside the range 1-100.</exception>
        public JObject listTransaction(int order = -1, int limit = 10, int offset = 0, int createdAtMin = 0,
            int createdAtMax = 0, string filterCustomData = "", string filterDecision = "", string filterDocupass = "",
            string filterProfileId = "")
        {
            if (order != 1 && order != -1)
            {
                throw new InvalidArgumentException("'order' should be integer of 1 or -1.");
            }

            if (limit <= 0 || limit > 100)
            {
                throw new InvalidArgumentException(
                    "'limit' should be a positive integer greater than 0 and less than or equal to 100.");
            }

            var payload = new Hashtable()
        {
            { "order", order },
            { "limit", limit },
            { "offset", offset },
        };

            if (createdAtMin > 0)
            {
                payload["createdAtMin"] = createdAtMin;
            }

            if (createdAtMax > 0)
            {
                payload["createdAtMax"] = createdAtMax;
            }

            if (filterCustomData != "")
            {
                payload["customData"] = filterCustomData;
            }

            if (filterDocupass != "")
            {
                payload["docupass"] = filterDocupass;
            }

            if (filterDecision != "")
            {
                payload["decision"] = filterDecision;
            }

            if (filterProfileId != "")
            {
                payload["profileId"] = filterProfileId;
            }

            var resp = this.sess.GetAsync(Common.GetEndpoint("transaction", payload)).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>
        /// Update transaction decision, updated decision will be relayed to webhook if set.
        /// </summary>
        /// <param name="transactionId">Transaction ID</param>
        /// <param name="decision">New decision (accept, review or reject)</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the transaction ID is empty or the decision is not accept, review or reject.</exception>
        public JObject updateTransaction(string transactionId = "", string decision = "")
        {
            if (transactionId == "")
            {
                throw new InvalidArgumentException("Transaction ID required.");
            }

            if (decision != "accept" && decision != "review" && decision != "reject")
            {
                throw new InvalidArgumentException("'decision' should be either accept, review or reject.");
            }

            var payload = new Hashtable()
        {
            { "decision", decision },
        };
            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var resp = this.sess.PatchAsync(Common.GetEndpoint(String.Format("transaction/{0}", transactionId)), content)
                .Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>
        /// Delete a transaction
        /// </summary>
        /// <param name="transactionId">Transaction ID</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the transaction ID is empty.</exception>
        public JObject deleteTransaction(string transactionId = "")
        {
            if (transactionId == "")
            {
                throw new InvalidArgumentException("Transaction ID required.");
            }

            var resp = this.sess.DeleteAsync(Common.GetEndpoint(String.Format("transaction/{0}", transactionId))).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>
        /// Download transaction image onto local file system
        /// </summary>
        /// <param name="imageToken">Image token from transaction API response</param>
        /// <param name="destination">Full destination path including file name, file extension should be jpg, for example: '\home\idcard.jpg'</param>
        /// <exception cref="InvalidArgumentException">Thrown when the image token or destination is empty.</exception>
        public void saveImage(string imageToken = "", string destination = "")
        {
            if (imageToken == "")
            {
                throw new InvalidArgumentException("'imageToken' required.");
            }

            if (destination == "")
            {
                throw new InvalidArgumentException("'destination' required.");
            }

            var resp = this.sess.GetAsync(Common.GetEndpoint(String.Format("imagevault/{0}", imageToken))).Result;
            File.WriteAllBytes(destination, resp.Content.ReadAsByteArrayAsync().Result);
        }

        /// <summary>
        /// Download transaction file onto local file system using secured file name obtained from transaction
        /// </summary>
        /// <param name="fileName">Secured file name</param>
        /// <param name="destination">Full destination path including file name, for example: '\home\auditreport.pdf'</param>
        /// <exception cref="InvalidArgumentException">Thrown when the file name or destination is empty.</exception>
        public void saveFile(string fileName = "", string destination = "")
        {
            if (fileName == "")
            {
                throw new InvalidArgumentException("'fileName' required.");
            }

            if (destination == "")
            {
                throw new InvalidArgumentException("'destination' required.");
            }

            var resp = this.sess.GetAsync(Common.GetEndpoint(String.Format("filevault/{0}", fileName))).Result;
            File.WriteAllBytes(destination, resp.Content.ReadAsByteArrayAsync().Result);
        }

        /// <summary>
        /// Download transaction archive onto local file system
        /// </summary>
        /// <param name="destination">Full destination path including file name, file extension should be zip, for example: '\home\archive.zip'</param>
        /// <param name="transactionId">Export only the specified transaction IDs</param>
        /// <param name="exportType">'csv' or 'json'</param>
        /// <param name="ignoreUnrecognized">Ignore unrecognized documents</param>
        /// <param name="ignoreDuplicate">Ignore duplicated entries</param>
        /// <param name="createdAtMin">Export only transactions that were created after this timestamp</param>
        /// <param name="createdAtMax">Export only transactions that were created before this timestamp</param>
        /// <param name="filterCustomData">Filter export by customData field</param>
        /// <param name="filterDecision">Filter export by decision (accept, review, reject)</param>
        /// <param name="filterDocupass">Filter export by Docupass reference</param>
        /// <param name="filterProfileId">Filter export by KYC Profile ID</param>
        /// <exception cref="InvalidArgumentException">Thrown when the destination is empty or exportType is not 'csv' or 'json'.</exception>
        public void exportTransaction(string destination = "", List<string>? transactionId = null,
            string exportType = "csv", bool ignoreUnrecognized = false, bool ignoreDuplicate = false, int createdAtMin = 0,
            int createdAtMax = 0, string filterCustomData = "", string filterDecision = "", string filterDocupass = "",
            string filterProfileId = "")
        {
            if (transactionId == null)
            {
                transactionId = new List<string>();
            }

            if (destination == "")
            {
                throw new InvalidArgumentException("'destination' required.");
            }

            if (exportType != "csv" && exportType != "json")
            {
                throw new InvalidArgumentException("'exportType' should be either 'json' or 'csv'.");
            }

            var payload = new Hashtable()
        {
            { "exportType", exportType },
            { "ignoreUnrecognized", ignoreUnrecognized },
            { "ignoreDuplicate", ignoreDuplicate },
        };

            if (transactionId.Count > 0)
            {
                payload["transactionId"] = transactionId;
            }

            if (createdAtMin > 0)
            {
                payload["createdAtMin"] = createdAtMin;
            }

            if (createdAtMax > 0)
            {
                payload["createdAtMax"] = createdAtMax;
            }

            if (filterCustomData != "")
            {
                payload["customData"] = filterCustomData;
            }

            if (filterDocupass != "")
            {
                payload["docupass"] = filterDocupass;
            }

            if (filterDecision != "")
            {
                payload["decision"] = filterDecision;
            }

            if (filterProfileId != "")
            {
                payload["profileId"] = filterProfileId;
            }

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var resp = this.sess.PostAsync(Common.GetEndpoint("export/transaction"), content).Result;
            var respJson = JObject.Parse(resp.Content.ReadAsStringAsync().Result);
            if (respJson.ContainsKey("Url"))
            {
                resp = this.sess.GetAsync(Common.GetEndpoint(respJson["Url"].ToString())).Result;
                File.WriteAllBytes(destination, resp.Content.ReadAsByteArrayAsync().Result);
            }
        }
    }

    /// <summary>
    /// Client for managing Docupass remote verification sessions: list, create, retrieve and delete.
    /// </summary>
    public class Docupass : ApiParent
    {
        /// <summary>
        /// Initialize the Docupass client.
        /// </summary>
        /// <param name="apiKey">Your API key. If null, the IDANALYZER_KEY environment variable is used.</param>
        public Docupass(string? apiKey = null) : base(apiKey)
        {
        }

        /// <summary>
        /// Retrieve a list of Docupass verification sessions.
        /// </summary>
        /// <param name="order">Sort results by newest(-1) or oldest(1)</param>
        /// <param name="limit">Number of items to be returned per call</param>
        /// <param name="offset">Start the list from a particular entry index</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when 'order' is not 1 or -1, or 'limit' is outside the range 1-100.</exception>
        public JObject listDocupass(int order = -1, int limit = 10, int offset = 0)
        {
            if (order != 1 && order != -1)
            {
                throw new InvalidArgumentException("'order' should be integer of 1 or -1.");
            }

            if (limit <= 0 || limit >= 100)
            {
                throw new InvalidArgumentException(
                    "'limit' should be a positive integer greater than 0 and less than or equal to 100.");
            }

            var payload = new Hashtable()
        {
            { "order", order },
            { "limit", limit },
            { "offset", offset },
        };
            var resp = this.sess.GetAsync(Common.GetEndpoint("docupass", payload)).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>
        /// Create a new Docupass remote verification session.
        /// </summary>
        /// <param name="profile">KYC Profile ID (string) or Profile to apply to the verification. Required.</param>
        /// <param name="contractFormat">Generated contract format: pdf, docx or html.</param>
        /// <param name="contractGenerate">Contract template ID(s) to auto-generate on completion.</param>
        /// <param name="reusable">Whether the Docupass link can be used by multiple users.</param>
        /// <param name="contractPrefill">Key-value data used to autofill contract dynamic fields.</param>
        /// <param name="contractSign">Contract signing options.</param>
        /// <param name="customData">Arbitrary string to store with the resulting transaction.</param>
        /// <param name="language">Display language for the verification UI.</param>
        /// <param name="mode">Verification mode.</param>
        /// <param name="referenceDocument">Reference front document image to verify against.</param>
        /// <param name="referenceDocumentBack">Reference back document image to verify against.</param>
        /// <param name="referenceFace">Reference face image to verify against.</param>
        /// <param name="userPhone">User phone number.</param>
        /// <param name="verifyAddress">Expected address to match against the document.</param>
        /// <param name="verifyAge">Expected age range (e.g. 18-40) to verify.</param>
        /// <param name="verifyDOB">Expected date of birth to verify.</param>
        /// <param name="verifyDocumentNumber">Expected document number to verify.</param>
        /// <param name="verifyName">Expected full name to verify.</param>
        /// <param name="verifyPostcode">Expected postcode to verify.</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when no profile is provided.</exception>
        public JObject createDocupass(object? profile = null, string contractFormat = "pdf", string contractGenerate = "",
            bool reusable = false, string contractPrefill = "", string contractSign = "", string customData = "",
            string language = "", int mode = 0, string referenceDocument = "", string referenceDocumentBack = "",
            string referenceFace = "", string userPhone = "", string verifyAddress = "", string verifyAge = "",
            string verifyDOB = "", string verifyDocumentNumber = "", string verifyName = "", string verifyPostcode = "")
        {
            if (profile == null)
            {
                throw new InvalidArgumentException("Profile is required.");
            }

            var payload = new Hashtable()
        {
            { "mode", mode },
            { "profile", profile },
            { "contractFormat", contractFormat },
            { "contractGenerate", contractGenerate },
            { "reusable", reusable },
        };
            if (contractPrefill != "")
                payload["contractPrefill"] = contractPrefill;
            if (contractSign != "")
                payload["contractSign"] = contractSign;
            if (customData != "")
                payload["customData"] = customData;
            if (language != "")
                payload["language"] = language;
            if (referenceDocument != "")
                payload["referenceDocument"] = referenceDocument;
            if (referenceDocumentBack != "")
                payload["referenceDocumentBack"] = referenceDocumentBack;
            if (referenceFace != "")
                payload["referenceFace"] = referenceFace;
            if (userPhone != "")
                payload["userPhone"] = userPhone;
            if (verifyAddress != "")
                payload["verifyAddress"] = verifyAddress;
            if (verifyAge != "")
                payload["verifyAge"] = verifyAge;
            if (verifyDOB != "")
                payload["verifyDOB"] = verifyDOB;
            if (verifyDocumentNumber != "")
                payload["verifyDocumentNumber"] = verifyDocumentNumber;
            if (verifyName != "")
                payload["verifyName"] = verifyName;
            if (verifyPostcode != "")
                payload["verifyPostcode"] = verifyPostcode;
            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var resp = this.sess.PostAsync(Common.GetEndpoint("docupass"), content).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>
        /// Delete a Docupass verification session by reference.
        /// </summary>
        /// <param name="reference">Docupass reference ID</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the reference is empty.</exception>
        public JObject deleteDocupass(string reference = "")
        {
            if (reference == "")
                throw new InvalidArgumentException("'reference' is required.");

            var resp = this.sess.DeleteAsync(Common.GetEndpoint(String.Format("docupass/{0}", reference))).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>
        /// Retrieve a single Docupass record by reference.
        /// </summary>
        /// <param name="reference">Docupass reference ID</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the reference is empty.</exception>
        public JObject getDocupass(string reference = "")
        {
            if (reference == "")
                throw new InvalidArgumentException("'reference' is required.");

            var resp = this.sess.GetAsync(Common.GetEndpoint(String.Format("docupass/{0}", reference))).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }
    }

    /// <summary>
    /// Client for AML/PEP/sanctions screening via the v1 and v3 search endpoints.
    /// </summary>
    public class AML : ApiParent
    {
        /// <summary>
        /// Initialize the AML client.
        /// </summary>
        /// <param name="apiKey">Your API key. If null, the IDANALYZER_KEY environment variable is used.</param>
        public AML(string? apiKey = null) : base(apiKey)
        {
        }

        /// <summary>
        /// Search the AML database (v1 endpoint).
        /// </summary>
        /// <param name="name">Search AML database with person's name or business name</param>
        /// <param name="idNumber">Search AML database with document number</param>
        /// <param name="entity">0=Person, 1=Corporation/Legal Entity</param>
        /// <param name="country">Two digit ISO country code to filter by country/nationality</param>
        /// <param name="database">Optional list of databases to search, e.g. ["us_ofac","eu_fsf"]. If null all databases are searched.</param>
        /// <param name="birthYear">Filter by year of birth</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when neither 'name' nor 'idNumber' is provided.</exception>
        public JObject search(string name = "", string idNumber = "", int entity = 0, string country = "",
            string[]? database = null, string birthYear = "")
        {
            if (name == "" && idNumber == "")
            {
                throw new InvalidArgumentException("Either 'name' or 'idNumber' is required.");
            }

            var payload = new Hashtable()
        {
            { "entity", entity },
        };
            if (name != "") payload["name"] = name;
            if (idNumber != "") payload["idNumber"] = idNumber;
            if (country != "") payload["country"] = country;
            if (birthYear != "") payload["birthYear"] = birthYear;
            if (database != null && database.Length > 0) payload["database"] = database;

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var resp = this.sess.PostAsync(Common.GetEndpoint("aml"), content).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>
        /// Search the AML database (v3 endpoint). Provide either a free-text query or one or more entity IDs.
        /// </summary>
        /// <param name="text">Full-text query (name, alias, document/passport/tax/registration number, etc.)</param>
        /// <param name="id">One or more AML entity IDs separated by comma or newline (max 50)</param>
        /// <param name="limit">Number of results to return per page</param>
        /// <param name="page">Result page number</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when neither 'text' nor 'id' is provided.</exception>
        public JObject searchV3(string text = "", string id = "", int limit = 0, int page = 0)
        {
            if (text == "" && id == "")
            {
                throw new InvalidArgumentException("Either 'text' or 'id' is required.");
            }

            var payload = new Hashtable() { };
            if (text != "") payload["text"] = text;
            if (id != "") payload["id"] = id;
            if (limit > 0) payload["limit"] = limit;
            if (page > 0) payload["page"] = page;

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var resp = this.sess.PostAsync(Common.GetEndpoint("amlv3"), content).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }
    }

    /// <summary>
    /// Client for managing stored KYC profiles: list, get, create, update, delete and export.
    /// </summary>
    public class ProfileAPI : ApiParent
    {
        /// <summary>
        /// Initialize the ProfileAPI client.
        /// </summary>
        /// <param name="apiKey">Your API key. If null, the IDANALYZER_KEY environment variable is used.</param>
        public ProfileAPI(string? apiKey = null) : base(apiKey)
        {
        }

        /// <summary>
        /// Build the request body for create/update profile calls from a name and a profile source.
        /// </summary>
        /// <param name="name">Profile name; omitted from the body when empty.</param>
        /// <param name="profile">A <see cref="Profile"/> (its overrides are flattened into the body) or a <see cref="Hashtable"/>; null is allowed.</param>
        /// <returns>The request body as a <see cref="Hashtable"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when <paramref name="profile"/> is neither a <see cref="Profile"/> nor a <see cref="Hashtable"/>.</exception>
        private static Hashtable ProfileBody(string name, object? profile)
        {
            var body = new Hashtable();
            if (name != "") body["name"] = name;
            if (profile != null)
            {
                if (profile is Profile p)
                {
                    foreach (DictionaryEntry kv in p.profileOverride)
                    {
                        body[kv.Key] = kv.Value;
                    }
                }
                else if (profile is Hashtable ht)
                {
                    foreach (DictionaryEntry kv in ht)
                    {
                        body[kv.Key] = kv.Value;
                    }
                }
                else
                {
                    throw new InvalidArgumentException("'profile' should be a Profile object or Hashtable.");
                }
            }
            return body;
        }

        /// <summary>List KYC profiles.</summary>
        /// <param name="order">Sort results by newest(-1) or oldest(1)</param>
        /// <param name="limit">Number of items to be returned per call</param>
        /// <param name="offset">Start the list from a particular entry index</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when 'order' is not 1 or -1.</exception>
        public JObject listProfile(int order = -1, int limit = 10, int offset = 0)
        {
            if (order != 1 && order != -1)
            {
                throw new InvalidArgumentException("'order' should be integer of 1 or -1.");
            }

            var payload = new Hashtable()
        {
            { "order", order },
            { "limit", limit },
            { "offset", offset },
        };
            var resp = this.sess.GetAsync(Common.GetEndpoint("profile", payload)).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>Retrieve a single KYC profile.</summary>
        /// <param name="profileId">KYC Profile ID</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the profile ID is empty.</exception>
        public JObject getProfile(string profileId = "")
        {
            if (profileId == "") throw new InvalidArgumentException("'profileId' required.");
            var resp = this.sess.GetAsync(Common.GetEndpoint(String.Format("profile/{0}", profileId))).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>Create a new KYC profile.</summary>
        /// <param name="name">Profile name</param>
        /// <param name="profile">A Profile object (its overrides become the profile config) or a Hashtable</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the profile name is empty, or 'profile' is neither a Profile nor a Hashtable.</exception>
        public JObject createProfile(string name = "", object? profile = null)
        {
            if (name == "") throw new InvalidArgumentException("Profile name required.");
            var content = new StringContent(JsonConvert.SerializeObject(ProfileBody(name, profile)), Encoding.UTF8, "application/json");
            var resp = this.sess.PostAsync(Common.GetEndpoint("profile"), content).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>Update an existing KYC profile.</summary>
        /// <param name="profileId">KYC Profile ID</param>
        /// <param name="name">Profile name</param>
        /// <param name="profile">A Profile object (its overrides become the profile config) or a Hashtable</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the profile ID is empty, or 'profile' is neither a Profile nor a Hashtable.</exception>
        public JObject updateProfile(string profileId = "", string name = "", object? profile = null)
        {
            if (profileId == "") throw new InvalidArgumentException("'profileId' required.");
            var content = new StringContent(JsonConvert.SerializeObject(ProfileBody(name, profile)), Encoding.UTF8, "application/json");
            var resp = this.sess.PutAsync(Common.GetEndpoint(String.Format("profile/{0}", profileId)), content).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>Delete a KYC profile.</summary>
        /// <param name="profileId">KYC Profile ID</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the profile ID is empty.</exception>
        public JObject deleteProfile(string profileId = "")
        {
            if (profileId == "") throw new InvalidArgumentException("'profileId' required.");
            var resp = this.sess.DeleteAsync(Common.GetEndpoint(String.Format("profile/{0}", profileId))).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>Export a KYC profile (GET /export/profile/{id}).</summary>
        /// <param name="profileId">KYC Profile ID</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the profile ID is empty.</exception>
        public JObject exportProfile(string profileId = "")
        {
            if (profileId == "") throw new InvalidArgumentException("'profileId' required.");
            var resp = this.sess.GetAsync(Common.GetEndpoint(String.Format("export/profile/{0}", profileId))).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }
    }

    /// <summary>
    /// Client for inspecting and managing webhook delivery logs.
    /// </summary>
    public class Webhook : ApiParent
    {
        /// <summary>
        /// Initialize the Webhook client.
        /// </summary>
        /// <param name="apiKey">Your API key. If null, the IDANALYZER_KEY environment variable is used.</param>
        public Webhook(string? apiKey = null) : base(apiKey)
        {
        }

        /// <summary>List webhook delivery logs.</summary>
        /// <param name="order">Sort results by newest(-1) or oldest(1)</param>
        /// <param name="limit">Number of items to be returned per call</param>
        /// <param name="offset">Start the list from a particular entry index</param>
        /// <param name="ev">Filter by event name; empty for all events.</param>
        /// <param name="success">Filter by delivery success: 1 for success, 0 for failure, any other value for all.</param>
        /// <param name="createdAtMin">List deliveries created after this timestamp</param>
        /// <param name="createdAtMax">List deliveries created before this timestamp</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        public JObject listWebhook(int order = -1, int limit = 10, int offset = 0, string ev = "",
            int success = -1, string createdAtMin = "", string createdAtMax = "")
        {
            var payload = new Hashtable()
        {
            { "order", order },
            { "limit", limit },
            { "offset", offset },
        };
            if (ev != "") payload["event"] = ev;
            if (success == 0 || success == 1) payload["success"] = success;
            if (createdAtMin != "") payload["createdAtMin"] = createdAtMin;
            if (createdAtMax != "") payload["createdAtMax"] = createdAtMax;

            var resp = this.sess.GetAsync(Common.GetEndpoint("webhook", payload)).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>Resend a webhook delivery.</summary>
        /// <param name="webhookId">Webhook delivery ID</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the webhook ID is empty.</exception>
        public JObject resendWebhook(string webhookId = "")
        {
            if (webhookId == "") throw new InvalidArgumentException("'webhookId' required.");
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            var resp = this.sess.PostAsync(Common.GetEndpoint(String.Format("webhook/{0}", webhookId)), content).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }

        /// <summary>Delete a webhook delivery log.</summary>
        /// <param name="webhookId">Webhook delivery ID</param>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        /// <exception cref="InvalidArgumentException">Thrown when the webhook ID is empty.</exception>
        public JObject deleteWebhook(string webhookId = "")
        {
            if (webhookId == "") throw new InvalidArgumentException("'webhookId' required.");
            var resp = this.sess.DeleteAsync(Common.GetEndpoint(String.Format("webhook/{0}", webhookId))).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }
    }

    /// <summary>
    /// Client for retrieving the current account profile, quota and usage.
    /// </summary>
    public class Account : ApiParent
    {
        /// <summary>
        /// Initialize the Account client.
        /// </summary>
        /// <param name="apiKey">Your API key. If null, the IDANALYZER_KEY environment variable is used.</param>
        public Account(string? apiKey = null) : base(apiKey)
        {
        }

        /// <summary>Retrieve current account profile, quota and usage.</summary>
        /// <returns>The API response as a <see cref="JObject"/>.</returns>
        public JObject getAccount()
        {
            var resp = this.sess.GetAsync(Common.GetEndpoint("myaccount")).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }
    }

    /// <summary>
    /// Exception thrown when an argument supplied to an SDK method is missing or invalid.
    /// </summary>
    public class InvalidArgumentException : Exception
    {
        /// <summary>Initialize a new <see cref="InvalidArgumentException"/> with no message.</summary>
        public InvalidArgumentException() { }

        /// <summary>Initialize a new <see cref="InvalidArgumentException"/> with the specified message.</summary>
        /// <param name="errMsg">The error message describing the invalid argument.</param>
        public InvalidArgumentException(string errMsg) : base(errMsg) { }
    }

    /// <summary>
    /// Exception thrown when the API returns an error response (and throw-on-error is enabled).
    /// </summary>
    public class ApiError : Exception
    {
        /// <summary>The API error message.</summary>
        public string Msg = "";

        /// <summary>The API error code.</summary>
        public string Code = "";

        /// <summary>Initialize a new <see cref="ApiError"/> with no message or code.</summary>
        public ApiError() { }

        /// <summary>Initialize a new <see cref="ApiError"/> with the specified message.</summary>
        /// <param name="errMsg">The API error message.</param>
        public ApiError(string errMsg) : base(errMsg)
        {
            this.Msg = errMsg;
        }

        /// <summary>Initialize a new <see cref="ApiError"/> with the specified message and code.</summary>
        /// <param name="errMsg">The API error message.</param>
        /// <param name="errCode">The API error code.</param>
        public ApiError(string errMsg, string errCode) : base(errMsg)
        {
            this.Msg = errMsg;
            this.Code = errCode;
        }
    }
}
