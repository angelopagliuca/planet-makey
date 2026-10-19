using System;
using System.IO;
using UnityEngine;

namespace PlanetMakey.Hits
{
    /// <summary>
    /// Holds the mapping from the wall sensor's positions to the projected picture, loads it
    /// when the game starts and saves it when a calibration finishes. Until a calibration
    /// exists, positions pass through unchanged.
    /// </summary>
    public sealed class WallCalibration : MonoBehaviour
    {
        private const int FileVersion = 1;

        [Tooltip("Name of the file the calibration is saved to, inside the game's persistent data folder.")]
        [SerializeField] private string _fileName = "wall-calibration.json";

        [NonSerialized] private Homography _homography;
        [NonSerialized] private bool _isCalibrated;
        [NonSerialized] private bool _isCalibrating;

        /// <summary>False until a calibration was loaded or finished, and after a reset.</summary>
        public bool IsCalibrated => _isCalibrated;

        /// <summary>
        /// True while a calibration is running. Hits thrown at the calibration targets are
        /// calibration points, not shots, so the hit resolver ignores them.
        /// </summary>
        public bool IsCalibrating => _isCalibrating;

        private void OnEnable()
        {
            _isCalibrating = false;
            Load();
        }

        private void OnDisable()
        {
            _isCalibrating = false;
        }

        /// <summary>Maps a sensor position to a position in the projected picture.</summary>
        public Vector2 Apply(Vector2 sensorPosition)
        {
            if (!_isCalibrated)
            {
                return sensorPosition;
            }

            return _homography.Apply(sensorPosition);
        }

        /// <summary>Marks a calibration as running, so hits are not treated as shots.</summary>
        public void BeginCalibration()
        {
            _isCalibrating = true;
        }

        /// <summary>Ends a calibration without changing the current mapping.</summary>
        public void CancelCalibration()
        {
            _isCalibrating = false;
        }

        /// <summary>
        /// Uses the new mapping from now on, saves it, and ends the calibration.
        /// </summary>
        /// <returns>True if the mapping was also saved to disk.</returns>
        public bool CompleteCalibration(Homography result)
        {
            _homography = result;
            _isCalibrated = true;
            _isCalibrating = false;

            return Save();
        }

        /// <summary>
        /// Goes back to passing positions through unchanged and deletes the saved file.
        /// </summary>
        [ContextMenu("Reset Calibration")]
        public void ResetCalibration()
        {
            _isCalibrated = false;
            _isCalibrating = false;

            string path = GetPath();
            if (path == null || !File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
                Debug.Log("Deleted wall calibration " + path, this);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogError("Could not delete the wall calibration " + path + ": " + exception.Message, this);
            }
        }

        // The file is the source of truth. With domain and scene reload off, state kept in memory
        // could be stale, so it is never trusted over the file. A calibration that could not be saved
        // is lost when the component is re-enabled; the operator was told it was not saved.
        private void Load()
        {
            _isCalibrated = false;

            string path = GetPath();
            if (path == null || !File.Exists(path))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                CalibrationFile file = JsonUtility.FromJson<CalibrationFile>(json);

                if (file != null
                    && file.Version == FileVersion
                    && Homography.TryFromArray(file.Matrix, out Homography loaded))
                {
                    _homography = loaded;
                    _isCalibrated = true;
                    Debug.Log("Loaded wall calibration " + path, this);
                    return;
                }

                Debug.LogWarning("The wall calibration file is not valid, so hits use raw positions: " + path, this);
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                Debug.LogWarning("Could not read the wall calibration " + path + ": " + exception.Message, this);
            }
        }

        private bool Save()
        {
            string path = GetPath();
            if (path == null)
            {
                Debug.LogWarning("WallCalibration has no file name, so the calibration is not saved.", this);
                return false;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string json = JsonUtility.ToJson(new CalibrationFile(FileVersion, _homography.ToArray()));
                File.WriteAllText(path, json);
                Debug.Log("Saved wall calibration " + path, this);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogError("Could not save the wall calibration " + path + ": " + exception.Message, this);
                return false;
            }
        }

        // Only the file name is used, so a value like "..\x.json" cannot leave the data folder.
        private string GetPath()
        {
            string name = string.IsNullOrWhiteSpace(_fileName) ? null : Path.GetFileName(_fileName.Trim());
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            return Path.Combine(Application.persistentDataPath, name);
        }

        // What is written to disk. The version lets a later change to the format be detected.
        [Serializable]
        private sealed class CalibrationFile
        {
            [SerializeField] private int _version;
            [SerializeField] private float[] _matrix;

            public CalibrationFile()
            {
            }

            public CalibrationFile(int version, float[] matrix)
            {
                _version = version;
                _matrix = matrix;
            }

            public int Version => _version;

            public float[] Matrix => _matrix;
        }
    }
}
