using UnityEngine;

namespace Groundwork.Renderer
{
    /// <summary>
    /// Orthographic camera controller for the map view. Supports both touch
    /// (pinch-to-zoom, single-finger drag) and mouse/keyboard (scroll wheel,
    /// right-click drag, WASD/arrows). Clamped to map boundaries.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Pan Settings")]
        public float panSpeed = 15f;
        public float edgePanSpeed = 25f;
        public float edgePanThreshold = 0.05f;

        [Header("Zoom Settings")]
        public float zoomSpeed = 8f;
        public float minZoom = 5f;
        public float maxZoom = 80f;

        [Header("Map Bounds")]
        public Vector2 mapCenter = new Vector2(50f, 50f);
        public Vector2 mapSize = new Vector2(100f, 100f);

        private Camera _cam;

        // Mouse state
        private Vector3 _lastMousePos;
        private bool _isDragging;

        // Touch state
        private bool _isTouchDragging;
        private Vector2 _lastTouchPos;
        private float _lastPinchDistance;
        private bool _wasPinching;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;

            _cam.orthographicSize = 40f;
            _cam.transform.position = new Vector3(mapCenter.x, 60f, mapCenter.y);
            _cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 200f;
        }

        void Update()
        {
            if (Input.touchSupported && Input.touchCount > 0)
            {
                HandleTouchPan();
                HandlePinchZoom();
            }
            else
            {
                HandleScrollZoom();
                HandleDragPan();
                HandleEdgePan();
                HandleKeyboardPan();
            }
            ClampToBounds();
        }

        // ═══════════════════════════════════════════
        //  Touch: single-finger drag → pan
        // ═══════════════════════════════════════════

        void HandleTouchPan()
        {
            // Only single-touch for pan (pinch uses 2 fingers)
            if (Input.touchCount != 1)
            {
                _isTouchDragging = false;
                return;
            }

            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                _isTouchDragging = true;
                _lastTouchPos = touch.position;
                return;
            }

            if (!_isTouchDragging) return;

            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                Vector2 delta = touch.position - _lastTouchPos;
                _lastTouchPos = touch.position;

                float scale = _cam.orthographicSize * 2f / Screen.height;
                Vector3 move = new Vector3(-delta.x * scale, 0, -delta.y * scale);
                transform.Translate(move, Space.World);
            }

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                _isTouchDragging = false;
            }
        }

        // ═══════════════════════════════════════════
        //  Touch: two-finger pinch → zoom
        // ═══════════════════════════════════════════

        void HandlePinchZoom()
        {
            if (Input.touchCount != 2)
            {
                _wasPinching = false;
                return;
            }

            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            float currentDistance = Vector2.Distance(t0.position, t1.position);

            if (!_wasPinching)
            {
                _lastPinchDistance = currentDistance;
                _wasPinching = true;
                return;
            }

            float delta = _lastPinchDistance - currentDistance;
            _lastPinchDistance = currentDistance;

            // Scale zoom speed by current zoom level and screen size
            float zoomFactor = delta / Screen.height * zoomSpeed * 2f;
            _cam.orthographicSize += zoomFactor;
            _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize, minZoom, maxZoom);
        }

        // ═══════════════════════════════════════════
        //  Mouse: scroll wheel → zoom
        // ═══════════════════════════════════════════

        void HandleScrollZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                _cam.orthographicSize -= scroll * zoomSpeed;
                _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize, minZoom, maxZoom);
            }
        }

        // ═══════════════════════════════════════════
        //  Mouse: right-click drag → pan
        // ═══════════════════════════════════════════

        void HandleDragPan()
        {
            if (Input.GetMouseButtonDown(1))
            {
                _isDragging = true;
                _lastMousePos = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(1))
            {
                _isDragging = false;
            }

            if (_isDragging)
            {
                Vector3 delta = Input.mousePosition - _lastMousePos;
                _lastMousePos = Input.mousePosition;

                float scale = _cam.orthographicSize * 2f / Screen.height;
                Vector3 move = new Vector3(-delta.x * scale, 0, -delta.y * scale);
                transform.Translate(move, Space.World);
            }
        }

        // ═══════════════════════════════════════════
        //  Mouse: edge-of-screen pan (desktop only)
        // ═══════════════════════════════════════════

        void HandleEdgePan()
        {
            Vector3 mouse = Input.mousePosition;
            Vector3 move = Vector3.zero;

            if (mouse.x < Screen.width * edgePanThreshold)
                move.x -= edgePanSpeed * Time.deltaTime * (_cam.orthographicSize / 10f);
            if (mouse.x > Screen.width * (1f - edgePanThreshold))
                move.x += edgePanSpeed * Time.deltaTime * (_cam.orthographicSize / 10f);
            if (mouse.y < Screen.height * edgePanThreshold)
                move.z -= edgePanSpeed * Time.deltaTime * (_cam.orthographicSize / 10f);
            if (mouse.y > Screen.height * (1f - edgePanThreshold))
                move.z += edgePanSpeed * Time.deltaTime * (_cam.orthographicSize / 10f);

            if (move != Vector3.zero)
                transform.Translate(move, Space.World);
        }

        // ═══════════════════════════════════════════
        //  Keyboard: WASD / arrows → pan
        // ═══════════════════════════════════════════

        void HandleKeyboardPan()
        {
            Vector3 move = Vector3.zero;
            float speed = panSpeed * Time.deltaTime * (_cam.orthographicSize / 10f);

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                move.z += speed;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                move.z -= speed;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                move.x -= speed;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                move.x += speed;

            if (move != Vector3.zero)
                transform.Translate(move, Space.World);
        }

        void ClampToBounds()
        {
            float halfHeight = _cam.orthographicSize;
            float halfWidth = halfHeight * _cam.aspect;

            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, -halfWidth, mapSize.x + halfWidth);
            pos.z = Mathf.Clamp(pos.z, -halfHeight, mapSize.y + halfHeight);
            transform.position = pos;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(
                new Vector3(mapCenter.x, 0, mapCenter.y),
                new Vector3(mapSize.x, 0, mapSize.y));
        }
    }
}