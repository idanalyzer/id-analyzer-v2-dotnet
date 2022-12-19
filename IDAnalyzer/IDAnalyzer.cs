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
    public class Common
    {
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
                return String.Format("https://v2-us1.idanalyzer.com/{0}{1}", uri, paramStr != "" ? "?" + paramStr : "");
            }

            if (region.ToLower() == "eu")
            {
                return String.Format("https://api2-eu.idanalyzer.com/{0}{1}", uri, paramStr != "" ? "?" + paramStr : "");
            }
            return String.Format("https://v2-us1.idanalyzer.com/{0}{1}", uri, paramStr != "" ? "?" + paramStr : "");
        }

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

    public class ApiParent
    {
        protected string? apiKey = null;
        protected string client_lib = "csharp-sdk";
        protected Hashtable config = new Hashtable();
        protected bool throwError = false;
        protected HttpClient sess = new HttpClient();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="apiKey">You API key</param>
        /// <exception cref="Exception">Please set API key via environment variable 'IDANALYZER_KEY'</exception>
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

    public class Profile
    {
        public static string SECURITY_NONE = "security_none";
        public static string SECURITY_LOW = "security_low";
        public static string SECURITY_MEDIUM = "security_medium";
        public static string SECURITY_HIGH = "security_high";

        private string URL_VALIDATION_REGEX =
            "((?:[a-z][\\w-]+:(?:/{1,3}|[a-z0-9%])|www\\d{0,3}[.]|[a-z0-9.\\-]+[.][a-z]{2,4}/)(?:[^\\s()<>]+|\\(([^\\s()<>]+|(\\([^\\s()<>]+\\)))*\\))+(?:\\(([^\\s()<>]+|(\\([^\\s()<>]+\\)))*\\)|[^\\s`!()\\[\\]{};:'\".,<>?«»“”‘’]))";

        public string profileId = "";
        public Hashtable profileOverride = new Hashtable();

        /// <summary>
        /// Initialize KYC Profile
        /// </summary>
        /// <param name="profileId">Custom profile ID or preset profile (security_none, security_low, security_medium, security_high). SECURITY_NONE will be used if left blank.</param>
        public Profile(string profileId = "")
        {
            this.profileId = profileId == "" ? profileId : SECURITY_NONE;
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
        /// <param name="pixels"></param>
        public void canvasSize(int pixels)
        {
            this.profileOverride["canvasSize"] = pixels;
        }

        /// <summary>
        /// Correct image orientation for rotated images
        /// </summary>
        /// <param name="enabled"></param>
        public void orientationCorrection(bool enabled)
        {
            this.profileOverride["orientationCorrection"] = enabled;
        }

        /// <summary>
        /// Enable to automatically detect and return the locations of signature, document and face.
        /// </summary>
        /// <param name="enabled"></param>
        public void objectDetection(bool enabled)
        {
            this.profileOverride["objectDetection"] = enabled;
        }

        /// <summary>
        /// Enable to parse AAMVA barcode for US/CA ID/DL. Disable this to improve performance if you are not planning on scanning ID/DL from US or Canada.
        /// </summary>
        /// <param name="enabled"></param>
        public void AAMVABarcodeParsing(bool enabled)
        {
            this.profileOverride["AAMVABarcodeParsing"] = enabled;
        }

        /// <summary>
        /// Whether scan transaction results and output images should be saved on cloud
        /// </summary>
        /// <param name="enableSaveTransaction"></param>
        /// <param name="enableSaveTransactionImages"></param>
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
        /// <param name="enableOutputImage"></param>
        /// <param name="outputFormat"></param>
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
        /// <param name="enableAutoCrop"></param>
        /// <param name="enableAdvancedAutoCrop"></param>
        public void autoCrop(bool enableAutoCrop, bool enableAdvancedAutoCrop)
        {
            this.profileOverride["crop"] = enableAutoCrop;
            this.profileOverride["advancedCrop"] = enableAdvancedAutoCrop;
        }

        /// <summary>
        /// Maximum width/height in pixels for output and saved image.
        /// </summary>
        /// <param name="pixels"></param>
        public void outputSize(int pixels)
        {
            this.profileOverride["outputSize"] = pixels;
        }

        /// <summary>
        /// Generate a full name field using parsed first name, middle name and last name.
        /// </summary>
        /// <param name="enabled"></param>
        public void inferFullName(bool enabled)
        {
            this.profileOverride["inferFullName"] = enabled;
        }

        /// <summary>
        /// If first name contains more than one word, move second word onwards into middle name field.
        /// </summary>
        /// <param name="enabled"></param>
        public void splitFirstName(bool enabled)
        {
            this.profileOverride["splitFirstName"] = enabled;
        }

        /// <summary>
        /// Enable to generate a detailed PDF audit report for every transaction.
        /// </summary>
        /// <param name="enabled"></param>
        public void transactionAuditReport(bool enabled)
        {
            this.profileOverride["transactionAuditReport"] = enabled;
        }

        /// <summary>
        /// Set timezone for audit reports. If left blank, UTC will be used. Refer to https://en.wikipedia.org/wiki/List_of_tz_database_time_zones TZ database name list.
        /// </summary>
        /// <param name="timezone"></param>
        public void setTimezone(string timezone)
        {
            this.profileOverride["timezone"] = timezone;
        }

        /// <summary>
        /// A list of data fields key to be redacted before transaction storage, these fields will also be blurred from output & saved image.
        /// </summary>
        /// <param name="fieldKeys"></param>
        public void obscure(string[] fieldKeys)
        {
            this.profileOverride["obscure"] = fieldKeys;
        }

        /// <summary>
        /// Enter a server URL to listen for Docupass verification and scan transaction results
        /// </summary>
        /// <param name="url"></param>
        /// <exception cref="InvalidArgumentException"></exception>
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

            if (uri.Scheme != "http" || uri.Scheme != "https")
            {
                throw new InvalidArgumentException("Invalid URL, only http and https protocols are allowed.");
            }

            this.profileOverride["webhook"] = url;
        }

        /// <summary>
        /// Set validation threshold of a specified component
        /// </summary>
        /// <param name="thresholdKey"></param>
        /// <param name="thresholdValue"></param>
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

    public class Biometric : ApiParent
    {
        public Biometric(string? apiKey = null) : base(apiKey)
        {
            this.config["profile"] = "";
            this.config["profileOverride"] = new Hashtable();
            this.config["customData"] = "";
        }

        /// <summary>
        /// Set an arbitrary string you wish to save with the transaction. e.g Internal customer reference number
        /// </summary>
        /// <param name="customData"></param>
        public void setCustomData(string customData)
        {
            this.config["customData"] = customData;
        }

        /// <summary>
        /// Set KYC Profile
        /// </summary>
        /// <param name="profile">KYCProfile object</param>
        /// <exception cref="InvalidArgumentException"></exception>
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
        /// <returns></returns>
        /// <exception cref="InvalidArgumentException"></exception>
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
        /// <returns></returns>
        /// <exception cref="InvalidArgumentException"></exception>
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

    public class Contract : ApiParent
    {
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
        /// <returns></returns>
        /// <exception cref="InvalidArgumentException"></exception>
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
        /// <returns></returns>
        /// <exception cref="InvalidArgumentException"></exception>
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
        /// <returns></returns>
        /// <exception cref="InvalidArgumentException"></exception>
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
        /// <returns></returns>
        /// <exception cref="InvalidArgumentException"></exception>
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
        /// <returns></returns>
        /// <exception cref="InvalidArgumentException"></exception>
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
        /// <returns></returns>
        /// <exception cref="InvalidArgumentException"></exception>
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

    public class Scanner : ApiParent
    {
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
        /// <param name="ip"></param>
        public void setUserIp(string ip)
        {
            this.config["ip"] = ip;
        }

        /// <summary>
        /// Set an arbitrary string you wish to save with the transaction. e.g Internal customer reference number
        /// </summary>
        /// <param name="customData"></param>
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
        /// <exception cref="InvalidArgumentException"></exception>
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
        /// <exception cref="InvalidArgumentException"></exception>
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
                if (DateTime.TryParseExact(dob, "yyyy/mm/dd", CultureInfo.CurrentCulture, DateTimeStyles.None,
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
        /// Initiate a new identity document scan & ID face verification transaction by providing input images.
        /// </summary>
        /// <param name="documentFront">Front of Document (file path, base64 content, url, or cache reference)</param>
        /// <param name="documentBack">Back of Document (file path, base64 content or URL, or cache reference)</param>
        /// <param name="facePhoto">Face Photo (file path, base64 content or URL, or cache reference)</param>
        /// <param name="faceVideo">Face Video (file path, base64 content or URL)</param>
        /// <returns></returns>
        /// <exception cref="InvalidArgumentException"></exception>
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
        /// <returns></returns>
        /// <exception cref="InvalidArgumentException"></exception>
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
    }

    public class Transaction : ApiParent
    {
        public Transaction(string? apiKey = null) : base(apiKey)
        {
        }

        /// <summary>
        /// Retrieve a single transaction record
        /// </summary>
        /// <param name="transactionId">Transaction ID</param>
        /// <returns></returns>
        /// <exception cref="InvalidArgumentException"></exception>
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
        /// <returns></returns>
        /// <exception cref="InvalidArgumentException"></exception>
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
        /// <returns></returns>
        /// <exception cref="InvalidArgumentException"></exception>
        public JObject updateTransaction(string transactionId = "", string decision = "")
        {
            if (transactionId == "")
            {
                throw new InvalidArgumentException("Transaction ID required.");
            }

            if (decision != "accept" && decision != "review" && decision == "reject")
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
        /// <returns></returns>
        /// <exception cref="InvalidArgumentException"></exception>
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
        /// <exception cref="InvalidArgumentException"></exception>
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
        /// <exception cref="InvalidArgumentException"></exception>
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
        /// <param name="transactionId">'csv' or 'json'</param>
        /// <param name="exportType">Ignore unrecognized documents</param>
        /// <param name="ignoreUnrecognized">Ignore duplicated entries</param>
        /// <param name="ignoreDuplicate">Export only the specified transaction IDs</param>
        /// <param name="createdAtMin">Export only transactions that were created after this timestamp</param>
        /// <param name="createdAtMax">Export only transactions that were created before this timestamp</param>
        /// <param name="filterCustomData">Filter export by customData field</param>
        /// <param name="filterDecision">Filter export by decision (accept, review, reject)</param>
        /// <param name="filterDocupass">Filter export by Docupass reference</param>
        /// <param name="filterProfileId">Filter export by KYC Profile ID</param>
        /// <exception cref="InvalidArgumentException"></exception>
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

    public class Docupass : ApiParent
    {
        public Docupass(string? apiKey = null) : base(apiKey)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="order"></param>
        /// <param name="limit"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        /// <exception cref="InvalidArgumentException"></exception>
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
        /// 
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="contractFormat"></param>
        /// <param name="contractGenerate"></param>
        /// <param name="reusable"></param>
        /// <param name="contractPrefill"></param>
        /// <param name="contractSign"></param>
        /// <param name="customData"></param>
        /// <param name="language"></param>
        /// <param name="mode"></param>
        /// <param name="referenceDocument"></param>
        /// <param name="referenceDocumentBack"></param>
        /// <param name="referenceFace"></param>
        /// <param name="userPhone"></param>
        /// <param name="verifyAddress"></param>
        /// <param name="verifyAge"></param>
        /// <param name="verifyDOB"></param>
        /// <param name="verifyDocumentNumber"></param>
        /// <param name="verifyName"></param>
        /// <param name="verifyPostcode"></param>
        /// <returns></returns>
        /// <exception cref="InvalidArgumentException"></exception>
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
        /// 
        /// </summary>
        /// <param name="reference"></param>
        /// <returns></returns>
        /// <exception cref="InvalidArgumentException"></exception>
        public JObject deleteDocupass(string reference = "")
        {
            if (reference == "")
                throw new InvalidArgumentException("'reference' is required.");

            var resp = this.sess.DeleteAsync(Common.GetEndpoint(String.Format("docupass/{0}", reference))).Result;
            return Common.ApiExceptionHandle(resp, this.throwError);
        }
    }

    public class InvalidArgumentException : Exception
    {
        public InvalidArgumentException() { }
        public InvalidArgumentException(string errMsg) : base(errMsg) { }
    }

    public class ApiError : Exception
    {
        public string Msg = "", Code = "";
        public ApiError() { }

        public ApiError(string errMsg) : base(errMsg)
        {
            this.Msg = errMsg;
        }

        public ApiError(string errMsg, string errCode) : base(errMsg)
        {
            this.Msg = errMsg;
            this.Code = errCode;
        }
    }
}
