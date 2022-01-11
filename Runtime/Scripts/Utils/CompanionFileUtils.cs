using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AR.Companion.Core
{
    static class CompanionFileUtils
    {
        static readonly string[] k_Base2Suffixes =
        {
            // EiB is the largest unit that can be described by a long count of bytes
            "B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB"
        };

        static void HandleIssue(bool isRead, Exception exception)
        {
            CompanionIssueUtils.HandleIssue(
                isRead ? CoreIssueCodes.CompanionFileRead : CoreIssueCodes.CompanionFileWrite,
                exception);
        }

        static void PreparePathForWrite(string path)
        {
            if (File.Exists(path))
                File.Delete(path);

            var directory = Path.GetDirectoryName(path);
            if (directory != null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        public static IEnumerator<string> ReadFileAsyncString(string path, Action<bool, string, string> callback = null)
        {
            string converted = null;

            void ConvertToString(bool success, string callbackPath, byte[] contents)
            {
                if (success)
                    converted = Encoding.UTF8.GetString(contents);

                callback?.Invoke(success, callbackPath, converted);
            }

            var enumerator = ReadFileAsyncBytes(path, ConvertToString);
            while (enumerator.MoveNext())
            {
                yield return null;
            }

            if (enumerator.Current == null)
                yield break;

            yield return converted;
        }

        static IEnumerator<byte[]> ReadFileAsyncBytes(string path, Action<bool, string, byte[]> callback)
        {
            Task task;
            FileStream file;
            byte[] contents = null;
            try
            {
                file = new FileStream(path, FileMode.Open);
                var length = (int)file.Length;
                contents = new byte[length];
                task = file.ReadAsync(contents, 0, length);
            }
            catch (Exception e)
            {
                Debug.Log($"Error reading file at {path}");
                Debug.LogException(e);
                callback?.Invoke(false, path, contents);
                HandleIssue(true, e);

                yield break;
            }

            while (!task.IsCanceled && !task.IsCompleted)
            {
                yield return contents;
            }

            Exception exception = task.Exception;
            if (exception != null)
                Debug.LogException(exception);

            try
            {
                file.Close();
            }
            catch (Exception e)
            {
                Debug.Log($"Error reading file at {path}");
                Debug.LogException(e);
                exception = e;
            }

            var success = exception == null;
            callback?.Invoke(success, path, contents);
            if (!success)
                HandleIssue(true, exception);
        }

        public static IEnumerator WriteFileAsync(string path, string contents, Action<bool, string> callback = null)
        {
            Task task;
            StreamWriter file = null;
            try
            {
                PreparePathForWrite(path);

                file = File.CreateText(path);
                task = file.WriteAsync(contents);
            }
            catch (Exception e)
            {
                Debug.Log($"Error writing file at {path}");
                file?.Close();
                Debug.LogException(e);
                callback?.Invoke(false, path);
                HandleIssue(false, e);

                yield break;
            }


            while (!task.IsCanceled && !task.IsCompleted)
            {
                yield return null;
            }

            Exception exception = task.Exception;
            if (exception != null)
                Debug.LogException(exception);

            try
            {
                file.Close();
            }
            catch (Exception e)
            {
                Debug.Log($"Error writing file at {path}");
                Debug.LogException(e);
                exception = e;
            }

            var success = exception == null;
            callback?.Invoke(success, path);
            if (!success)
                HandleIssue(false, exception);
        }

        public static IEnumerator WriteFileAsync(string path, byte[] contents, Action<bool, string> callback = null)
        {
            Task task;
            FileStream file;
            try
            {
                PreparePathForWrite(path);

                file = File.Create(path);
                task = file.WriteAsync(contents, 0, contents.Length);
            }
            catch (Exception e)
            {
                Debug.Log($"Error writing file at {path}");
                Debug.LogException(e);
                HandleIssue(false, e);
                callback?.Invoke(false, path);
                yield break;
            }

            while (!task.IsCanceled && !task.IsCompleted)
            {
                yield return null;
            }

            Exception exception = task.Exception;
            if (exception != null)
                Debug.LogException(exception);

            try
            {
                file.Close();
            }
            catch (Exception e)
            {
                Debug.Log($"Error writing file at {path}");
                Debug.LogException(e);
                exception = e;
            }

            var success = exception == null;
            callback?.Invoke(success, path);
            if (!success)
                HandleIssue(false, exception);
        }

        public static string GetReadableFileSize(long fileSize)
        {
            var number = (decimal)fileSize;

            var i = 0;
            while (number / 1024 >= 1)
            {
                number = number / 1024;
                i++;
            }

            if (number >= 100)
                return $"{number:#0} {k_Base2Suffixes[i]}";

            if (number >= 10)
                return $"{number:#0.#} {k_Base2Suffixes[i]}";

            return $"{number:0.##} {k_Base2Suffixes[i]}";
        }
    }
}
