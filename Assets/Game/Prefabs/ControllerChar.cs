using UnityEngine;

public class ControllerChar : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        CharacterController controller = GetComponent<CharacterController>();

        controller.Move(new Vector3(0, 0, 1) * Time.deltaTime);
    }
}
