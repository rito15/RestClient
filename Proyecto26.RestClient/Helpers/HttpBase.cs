﻿using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Proyecto26.Common;

namespace Proyecto26
{
    public static class HttpBase
    {
        public static IEnumerator CreateRequestAndRetry(RequestHelper options, Action<RequestException, ResponseHelper> callback)
        {
            var retries = 0;
            do
            {
                using (var request = CreateRequest(options))
                {
                    yield return request.SendWebRequestWithOptions(options);
                    var response = request.CreateWebResponse();
                    if (request.IsValidRequest(options))
                    {
                        DebugLog(options.EnableDebug, string.Format("Url: {0}\nMethod: {1}\nStatus: {2}\nResponse: {3}", options.Uri, options.Method, request.responseCode, options.ParseResponseBody ? response.Text : "body not parsed"), false);
                        callback(null, response);
                        break;
                    }
                    else if (!options.IsAborted && retries < options.Retries)
                    {
                        yield return new WaitForSeconds(options.RetrySecondsDelay);
                        retries++;
                        if(options.RetryCallback != null)
                        {
                            options.RetryCallback(CreateException(options, request), retries);
                        }
                        DebugLog(options.EnableDebug, string.Format("Retry Request\nUrl: {0}\nMethod: {1}", options.Uri, options.Method), false);
                    }
                    else
                    {
                        var err = CreateException(options, request);
                        DebugLog(options.EnableDebug, err, true);
                        callback(err, response);
                        break;
                    }
                }
            }
            while (retries <= options.Retries);
        }

        private static UnityWebRequest CreateRequest(RequestHelper options)
        {
            if (options.FormData is WWWForm && options.Method == UnityWebRequest.kHttpVerbPOST)
            {
                return UnityWebRequest.Post(options.Uri, options.FormData);
            }
            else
            {
                return new UnityWebRequest(options.Uri, options.Method);
            }
        }

        private static RequestException CreateException(RequestHelper options, UnityWebRequest request)
        {
            return new RequestException(request.error, request.isHttpError, request.isNetworkError, request.responseCode, options.ParseResponseBody ? request.downloadHandler.text : "body not parsed");
        }

        private static void DebugLog(bool debugEnabled, object message, bool isError)
        {
            if (debugEnabled)
            {
                if (isError)
                    Debug.LogError(message);
                else
                    Debug.Log(message);
            }
        }

        public static IEnumerator DefaultUnityWebRequest(RequestHelper options, Action<RequestException, ResponseHelper> callback)
        {
            return CreateRequestAndRetry(options, callback);
        }

        private static void ParseBodyAndExecuteCallback<TResponse>(
            RequestException err,
            ResponseHelper res,
            RequestHelper options,
            Action<RequestException, ResponseHelper, TResponse> callback)
        {
            var body = default(TResponse);
            try
            {
                if (err == null && res.Data != null && options.ParseResponseBody)
                    body = JsonUtility.FromJson<TResponse>(res.Text);
            }
            catch (Exception error)
            {
                DebugLog(options.EnableDebug, string.Format("Invalid JSON format\nError: {0}", error.Message), true);
                err = new RequestException(error.Message);
            }
            finally
            {
                callback(err, res, body);
            }
        }

        public static IEnumerator DefaultUnityWebRequest<TResponse>(RequestHelper options, Action<RequestException, ResponseHelper, TResponse> callback)
        {
            return CreateRequestAndRetry(options, (RequestException err, ResponseHelper res) => {
                ParseBodyAndExecuteCallback(err, res, options, callback);
            });
        }

        public static IEnumerator DefaultUnityWebRequest<TResponse>(RequestHelper options, Action<RequestException, ResponseHelper, TResponse[]> callback)
        {
            return CreateRequestAndRetry(options, (RequestException err, ResponseHelper res) =>
            {
                ParseBodyAndExecuteCallback(err, res, options, callback);
            });
        }
    }
}
