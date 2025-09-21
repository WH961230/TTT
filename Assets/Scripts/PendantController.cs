// using UnityEngine;
// using UnityEngine.UI;
//
// public class PendantController : MonoBehaviour {
//     public MousePendant2D mousePendant; // 通过 Inspector 将 MousePendant2D 拖入
//     public Button uploadButton; // 选择图片按钮
//     public Button resetButton; // 恢复默认按钮
//
//     public void Start() {
//         // 上传图片按钮
//         uploadButton.onClick.AddListener(() => { mousePendant.UploadAndApplyNewImage(); });
//
//         // 恢复默认按钮
//         resetButton.onClick.AddListener(() => { mousePendant.ResetToDefaultSprite(); });
//     }
// }