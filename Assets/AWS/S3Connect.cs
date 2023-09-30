using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using System.IO;
using System;
using Amazon.S3.Util;
using System.Collections.Generic;
using Amazon.CognitoIdentity;
using Amazon;

namespace AWSSDK.Examples
{
    public class S3Connect : MonoBehaviour
    {
        public string IdentityPoolId = "us-east-1:997dbb67-4c34-4702-8a6e-76e110397693";
        public string CognitoIdentityRegion = RegionEndpoint.USEast1.SystemName;
        private RegionEndpoint _CognitoIdentityRegion
        {
            get { return RegionEndpoint.GetBySystemName(CognitoIdentityRegion); }
        }
        public string S3Region = RegionEndpoint.USEast1.SystemName;
        private RegionEndpoint _S3Region
        {
            get { return RegionEndpoint.GetBySystemName(S3Region); }
        }
        public string S3BucketName = null;
        public string SampleFileName = null;
        public Button GetBucketListButton = null;
        public Button PostBucketButton = null;
        public Button GetObjectsListButton = null;
        public Button DeleteObjectButton = null;
        public Button GetObjectButton = null;
        public Text ResultText = null;

        void Start()
        {
            UnityInitializer.AttachToGameObject(this.gameObject);
            GetBucketListButton.onClick.AddListener(() => { GetBucketList(); });
            PostBucketButton.onClick.AddListener(() => { PostObject(); });
            GetObjectsListButton.onClick.AddListener(() => { GetObjects(); });
            DeleteObjectButton.onClick.AddListener(() => { DeleteObject(); });
            GetObjectButton.onClick.AddListener(() => { GetObject(); });
        }

        private IAmazonS3 _s3Client;
        private AWSCredentials _credentials;

        private AWSCredentials Credentials
        {
            get
            {
                if (_credentials == null)
                    _credentials = new CognitoAWSCredentials(IdentityPoolId, _CognitoIdentityRegion);
                return _credentials;
            }
        }

        private IAmazonS3 Client
        {
            get
            {
                if (_s3Client == null)
                {
                    _s3Client = new AmazonS3Client(Credentials, _S3Region);
                }
            
                return _s3Client;
            }
        }

        public void GetBucketList()
        {
            ResultText.text = "Fetching buckets.. ";
            Client.ListBucketsAsync(new ListBucketsRequest(), (responseObject) =>
            {
                ResultText.text += "\n";
                if (responseObject.Exception == null)
                {
                    ResultText.text += "Last Updates : \n";
                    responseObject.Response.Buckets.ForEach((s3b) =>
                    {
                        ResultText.text += string.Format("bucket = {0}, created date = {1} \n", s3b.BucketName, s3b.CreationDate);
                    });
                }
                else
                {
                    ResultText.text += "Got Exception \n";
                }
            });
        }

        private void GetObject()
        {
            ResultText.text = string.Format("fetching {0} from bucket {1}", SampleFileName, S3BucketName);
            Client.GetObjectAsync(S3BucketName, SampleFileName, (responseObj) =>
            {
                string data = null;
                var response = responseObj.Response;
                if (response.ResponseStream != null)
                {
                    using (StreamReader reader = new StreamReader(response.ResponseStream))
                    {
                        data = reader.ReadToEnd();
                    }

                    ResultText.text += "\n";
                    ResultText.text += data;
                }
            });
        }
        public void PostObject()
        {
            ResultText.text = "Retrieving the file";

            string fileName = GetFileHelper();
             
            var stream = new FileStream(Application.persistentDataPath + Path.DirectorySeparatorChar + fileName, FileMode.Open, FileAccess.Read, FileShare.Read);

            ResultText.text += "\nCreating request object";
            var request = new PostObjectRequest()
            {
                Bucket = S3BucketName,
                Key = fileName,
                InputStream = stream,
                CannedACL = S3CannedACL.Private
            };

            ResultText.text += "\nMaking HTTP post call";

            Client.PostObjectAsync(request, (responseObj) =>
            {
                if (responseObj.Exception == null)
                {
                    ResultText.text += string.Format("\nobject {0} posted to bucket {1}", responseObj.Request.Key, responseObj.Request.Bucket);
                }
                else
                {
                    ResultText.text += "\nException while posting the result object";
                    ResultText.text += string.Format("\n receieved error {0}", responseObj.Response.HttpStatusCode.ToString());
                }
            });
        }

        public void GetObjects()
        {
            ResultText.text = "Fetching Objects from " + S3BucketName;

            var request = new ListObjectsRequest()
            {
                BucketName = S3BucketName
            };

            Client.ListObjectsAsync(request, (responseObject) =>
            {
                ResultText.text += "\n";
                if (responseObject.Exception == null)
                {
                    ResultText.text += "Objects : ";
                    responseObject.Response.S3Objects.ForEach((o) =>
                    {
                        ResultText.text += string.Format("{0}\n", o.Key);
                    });
                }
                else
                {
                    ResultText.text += "Got Exception \n";
                }
            });
        }
        public void DeleteObject()
        {
            ResultText.text = string.Format("deleting {0} from bucket {1}", SampleFileName, S3BucketName);
            List<KeyVersion> objects = new List<KeyVersion>();
            objects.Add(new KeyVersion()
            {
                Key = SampleFileName
            });

            var request = new DeleteObjectsRequest()
            {
                BucketName = S3BucketName,
                Objects = objects
            };

            Client.DeleteObjectsAsync(request, (responseObj) =>
            {
                ResultText.text += "\n";
                if (responseObj.Exception == null)
                {
                    ResultText.text += "Got Response \n \n";

                    ResultText.text += string.Format("deleted objects \n");

                    responseObj.Response.DeletedObjects.ForEach((dObj) =>
                    {
                        ResultText.text += dObj.Key;
                    });
                }
                else
                {
                    ResultText.text += "Got Exception \n";
                }
            });
        }

        private string GetFileHelper()
        {
            var fileName = SampleFileName;

            if (!File.Exists(Application.persistentDataPath + Path.DirectorySeparatorChar + fileName))
            {
                var streamReader = File.CreateText(Application.persistentDataPath + Path.DirectorySeparatorChar + fileName);
                streamReader.WriteLine("File Uploaded Now ");
                streamReader.Close();
            }
            return fileName;
        }

        private string GetPostPolicy(string bucketName, string key, string contentType)
        {
            bucketName = bucketName.Trim();

            key = key.Trim();
         
            if (!string.IsNullOrEmpty(key) && key[0] == '/')
            {
                throw new ArgumentException("uploadFileName cannot start with / ");
            }

            contentType = contentType.Trim();

            if (string.IsNullOrEmpty(bucketName))
            {
                throw new ArgumentException("bucketName cannot be null or empty. It's required to build post policy");
            }
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("uploadFileName cannot be null or empty. It's required to build post policy");
            }
            if (string.IsNullOrEmpty(contentType))
            {
                throw new ArgumentException("contentType cannot be null or empty. It's required to build post policy");
            }

            string policyString = null;
            int position = key.LastIndexOf('/');
            if (position == -1)
            {
                policyString = "{\"expiration\": \"" + DateTime.UtcNow.AddHours(24).ToString("yyyy-MM-ddTHH:mm:ssZ") + "\",\"conditions\": [{\"bucket\": \"" +
                    bucketName + "\"},[\"starts-with\", \"$key\", \"" + "\"],{\"acl\": \"private\"},[\"eq\", \"$Content-Type\", " + "\"" + contentType + "\"" + "]]}";
            }
            else
            {
                policyString = "{\"expiration\": \"" + DateTime.UtcNow.AddHours(24).ToString("yyyy-MM-ddTHH:mm:ssZ") + "\",\"conditions\": [{\"bucket\": \"" +
                    bucketName + "\"},[\"starts-with\", \"$key\", \"" + key.Substring(0, position) + "/\"],{\"acl\": \"private\"},[\"eq\", \"$Content-Type\", " + "\"" + contentType + "\"" + "]]}";
            }

            return policyString;
        }

    }
}
