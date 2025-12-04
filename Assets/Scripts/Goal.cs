using UnityEngine;

public class Goal : MonoBehaviour
{
    [SerializeField] private int teamID;

    public void setTeamID(int id)
    {
        teamID = id;
    }

    
}
