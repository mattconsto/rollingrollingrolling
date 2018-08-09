using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CameraPosition : MonoBehaviour {
	public Camera camera;

	public Vector3 offset = new Vector3(2f, 7f, 0);
	public float rotation = 0f;

	public float rotationSpeed = 1f;
	public float lookSpeed = 1f;
	public float movementSpeed = 100f;
	public float jumpForce = 50f;

	public float floorDistance = 1f;
	public Vector2 lookLimits = new Vector2(0, 10f);

	public float totalMass = 0f;
	public float totalMerges = 0;

	private Rigidbody rb;

	public void Start() {
		// Lock the cursor
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;

		rb = GetComponent<Rigidbody>();

		totalMass += rb.mass;

		GenerateCollisionMesh();
	}

	public void LateUpdate() {
		// Camera
		rotation += Input.GetAxis("Mouse X") * rotationSpeed;
		rotation = (rotation + 360) % 360;

		offset.y = Mathf.Clamp(offset.y + Input.GetAxis("Mouse Y") * lookSpeed, lookLimits.x, lookLimits.y);
		offset.x = Mathf.Clamp(offset.x + Input.GetAxis("Mouse Y") * lookSpeed, lookLimits.x, lookLimits.y);

		camera.transform.position = transform.position + offset;
		camera.transform.LookAt(transform.position);
		camera.transform.RotateAround(transform.position, Vector3.up, rotation);
	}

	public void FixedUpdate() {
		// Movement
		float h = Input.GetAxis("Horizontal") * movementSpeed * Time.deltaTime;
		float v = Input.GetAxis("Vertical") * movementSpeed * Time.deltaTime;

		// Move in the direction we are looking at
		rb.AddTorque(new Vector3(camera.transform.forward.x, 0, camera.transform.forward.z) * -h);
		rb.AddTorque(new Vector3(camera.transform.right.x, 0, camera.transform.right.z) * v);

		// Jump
		RaycastHit hit;
		if(Input.GetButton("Jump") && Physics.Raycast(transform.position, -Vector3.up, out hit, floorDistance)) {
			rb.AddForce(Vector3.up * jumpForce);
		}
	}

	public void OnCollisionEnter(Collision collision) {
		if(collision.gameObject.CompareTag("Mergeable")) {
			collision.gameObject.transform.parent = transform;
			if(collision.rigidbody != null) {
				collision.rigidbody.isKinematic = true;
				totalMass += collision.rigidbody.mass;
			}
			if(collision.collider != null) collision.collider.enabled = false;
			totalMerges += 1;
			SetAllLayers(collision.gameObject.transform, LayerMask.NameToLayer("Player"));
		}
	}

	public static void SetAllLayers(Transform root, int layer) {
		Stack<Transform> children = new Stack<Transform>(new Transform[] {root});
		while(children.Count > 0) {
			Transform current = children.Pop();
			current.gameObject.layer = layer;
			foreach(Transform child in current) children.Push(child);
		}
	}

	private void GenerateCollisionMesh() {
		int resolution = 3;

		Vector3[] vertices = new Vector3[resolution * resolution];
		int[] triangles = new int[6 * resolution * (resolution - 1)];

		// Generate vertices
		for(int i = 0; i < resolution; i += 1) {
			for(int j = 0; j < resolution; j += 1) {
				float lat = 2f * Mathf.PI * (float)i / (float)resolution;
				float lon = 2f * Mathf.PI * (float)j / (float)resolution;

				// Convert lat/long to xyz, then get the normalised direction
				float cosLat = Mathf.Cos(lat), sinLat = Mathf.Sin(lat);
				float cosLon = Mathf.Cos(lon), sinLon = Mathf.Sin(lon);

				Vector3 direction = new Vector3(cosLat * cosLon, cosLat * sinLon, sinLat).normalized;

				// Find the outermost transform
				RaycastHit hit = Physics
					.RaycastAll(transform.position, direction, Mathf.Infinity, LayerMask.GetMask("Player"))
					.OrderByDescending(h => h.distance)
					.FirstOrDefault();

				vertices[i * resolution + j] = hit.point - transform.position; // Relative to this.

				Debug.Log("Vert:" + lat + "," + lon + " " + direction + " " + vertices[i * resolution + j]);
			}
		}

		// Generate Triangles
		for(int i = 0; i < resolution - 1; i += 1) {
			for(int j = 0; j < resolution; j += 1) {
				int offset = 6 * (i * resolution + j);

				triangles[offset + 0] = i * resolution + j;
				triangles[offset + 1] = i * resolution + ((j + 1) % resolution);
				triangles[offset + 2] = ((i + 1) % resolution) * resolution + j;

				triangles[offset + 3] = ((i + 1) % resolution) * resolution + ((j + 1) % resolution);
				triangles[offset + 4] = i * resolution + ((j + 1) % resolution);
				triangles[offset + 5] = ((i + 1) % resolution) * resolution + j;
			}
		}

		// Set up mesh
		Mesh mesh = new Mesh();
		mesh.name = "Player Mesh";
		mesh.vertices = vertices;
		mesh.triangles = triangles;
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
		GetComponent<MeshCollider>().sharedMesh = mesh;
		GetComponent<MeshFilter>().sharedMesh = mesh;
	}
}
