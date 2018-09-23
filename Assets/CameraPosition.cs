using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CameraPosition : MonoBehaviour {
	public Camera playerCamera;

	public Vector3 offset = new Vector3(2f, 7f, 0);
	public float rotation = 0f;

	public float rotationSpeed = 1f;
	public float lookSpeed = 1f;
	public float movementSpeed = 100f;
	public float jumpForce = 50f;

	public float floorDistance = 1f;
	public Vector2 lookLimits = new Vector2(0, 10f);

	public float totalMerges = 0;
	public float maxRadius = 0;

	private Rigidbody rb;

	public void Start() {
		// Lock the cursor
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;

		rb = GetComponent<Rigidbody>();

		GenerateCollisionMesh();
	}

	public void LateUpdate() {
		// playerCamera
		rotation += Input.GetAxis("Mouse X") * rotationSpeed;
		rotation = (rotation + 360) % 360;

		offset.y = Mathf.Clamp(offset.y + Input.GetAxis("Mouse Y") * lookSpeed, lookLimits.x, lookLimits.y);
		offset.x = Mathf.Clamp(offset.x + Input.GetAxis("Mouse Y") * lookSpeed, lookLimits.x, lookLimits.y);

		playerCamera.transform.position = transform.position + offset;
		playerCamera.transform.LookAt(transform.position);
		playerCamera.transform.RotateAround(transform.position, Vector3.up, rotation);
	}

	public void FixedUpdate() {
		// Movement
		float h = Input.GetAxis("Horizontal") * movementSpeed * Time.deltaTime;
		float v = Input.GetAxis("Vertical") * movementSpeed * Time.deltaTime;

		// Move in the direction we are looking at
		rb.AddTorque(new Vector3(playerCamera.transform.forward.x, 0, playerCamera.transform.forward.z) * -h);
		rb.AddTorque(new Vector3(playerCamera.transform.right.x, 0, playerCamera.transform.right.z) * v);

		// Jump
		RaycastHit hit;
		if(Input.GetButton("Jump") && Physics.Raycast(transform.position, -Vector3.up, out hit, floorDistance)) {
			rb.AddForce(Vector3.up * jumpForce);
		}
	}

	public void OnCollisionEnter(Collision collision) {
		if(collision.gameObject.CompareTag("Mergeable")) {
			if(collision.rigidbody != null) {
				// if(collision.rigidbody.mass > rb.mass) {
					// Debug.Log("Too massive to collect");
					// return;
				// }

				collision.rigidbody.isKinematic = true;
				rb.mass += collision.rigidbody.mass;
			}

			// 'Gravity'
			collision.transform.position = (collision.transform.position - transform.position) / 5f + transform.position;

			collision.transform.parent = transform.Find("Attached");

			totalMerges += 1;
			SetAllLayers(collision.transform, LayerMask.NameToLayer("Player"));
			GenerateCollisionMesh();
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
		Vector3 initialVelocity = rb.velocity;
		foreach(Transform child in transform.Find("Attached")) {
			if(child.gameObject.GetComponent<Collider>()) {
				child.gameObject.GetComponent<Collider>().enabled = true;
			}
		}

		int resolution = 12;

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
				IOrderedEnumerable<RaycastHit> hits = Physics
					.RaycastAll(transform.position + direction * 10000, -direction, 10000, LayerMask.GetMask("Player"))
					.OrderBy(h => h.distance);

				if(hits.Count() > 0) {
					maxRadius = Mathf.Max(maxRadius, 10000 - hits.First().distance);
					vertices[i * resolution + j] = hits.First().point - transform.position; // Relative to this.
					Debug.DrawRay(transform.position, hits.First().point - transform.position, Color.green);
				} else {
					Debug.DrawRay(transform.position, direction * 10000, Color.red);
				}
			}
		}

		// Generate Triangles
		// Doesn't work, but doesn't seem to be needed for colliders
		// for(int i = 0; i < resolution - 1; i += 1) {
		// 	for(int j = 0; j < resolution; j += 1) {
		// 		int offset = 6 * (i * resolution + j);

		// 		triangles[offset + 0] = i * resolution + j;
		// 		triangles[offset + 1] = i * resolution + ((j + 1) % resolution);
		// 		triangles[offset + 2] = ((i + 1) % resolution) * resolution + j;

		// 		triangles[offset + 3] = ((i + 1) % resolution) * resolution + ((j + 1) % resolution);
		// 		triangles[offset + 4] = i * resolution + ((j + 1) % resolution);
		// 		triangles[offset + 5] = ((i + 1) % resolution) * resolution + j;
		// 		break;
		// 	}
		// 	break;
		// }

		// Set up mesh
		Mesh mesh = new Mesh();
		mesh.name = "Player Mesh";
		mesh.vertices = vertices;
		mesh.triangles = triangles;
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
		GetComponent<MeshCollider>().sharedMesh = mesh;
		// GetComponent<MeshFilter>().sharedMesh = mesh;
		
		foreach(Transform child in transform.Find("Attached")) {
			if(child.gameObject.GetComponent<Collider>()) {
				child.gameObject.GetComponent<Collider>().enabled = false;
			}
		}

		// Avoid loosing speed.
		rb.velocity = initialVelocity;
	}
}
