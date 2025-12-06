using UnityEngine;

public class Goal : MonoBehaviour
{
    [SerializeField] private int teamID;

    public void setTeamID(int id)
    {
        teamID = id;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Ball"))
        {
            GameManager.Instance.AddScore(teamID, 1);
        }
    }
}
