"""
    Modified from:
        https://github.com/isl-org/MiDaS/blob/master/tf/make_onnx_model.py (MIT License, see ./midas/LICENSE)
    imitating https://github.com/isl-org/MiDaS/issues/182
    ONNX file this script creates is not official.
    Disclaimer: I don't know how this code works or whether this actually works.
"""
import os
import torch
import numpy as np

from shutil import copyfile
import sys
sys.path.append(os.getcwd() + '/..')
                 
def modify_file():
    ###############################################################################
    # Change vit.py too -> https://github.com/isl-org/MiDaS/issues/182
    ###############################################################################

    changes_dict = {
        "../midas/blocks.py": [
            ('align_corners=True', 'align_corners=False'),
            ('import torch.nn as nn', 'import torch.nn as nn\nimport torchvision.models as models'),
            ('torch.hub.load("facebookresearch/WSL-Images", "resnext101_32x8d_wsl")', 'models.resnext101_32x8d()')
        ],
        "../midas/vit.py": [
            (
"""
    unflatten = nn.Sequential(
        nn.Unflatten(
            2,
            torch.Size(
                [
                    h // pretrained.model.patch_size[1],
                    w // pretrained.model.patch_size[0],
                ]
            ),
        )
    )
""",
"""
    unflatten = lambda layer: layer.view((
        b,
        layer.shape[1],
        h // pretrained.model.patch_size[1],
        w // pretrained.model.patch_size[0]
    ))
"""
            )
        ]
    }

    for modify_filename, changes in changes_dict.items():
        
        copyfile(modify_filename, modify_filename+'.bak')

        with open(modify_filename, 'r') as file :
            filedata = file.read()

        for from_str, to_str in changes:
            filedata = filedata.replace(from_str, to_str)

        with open(modify_filename, 'w') as file:
            file.write(filedata)
      
def restore_file():
    modify_filenames = ["../midas/blocks.py", "../midas/vit.py"]
    for modify_filename in modify_filenames:
        copyfile(modify_filename+'.bak', modify_filename)

modify_file()

from midas.dpt_depth import DPTDepthModel

restore_file()

class DPTDepthModel_pp(DPTDepthModel):
    """Network for monocular depth estimation.
    """
    def forward(self, x):
        """Forward pass.

        Args:
            x (tensor): input data (image)

        Returns:
            tensor: depth
        """

        mean = torch.tensor([0.5, 0.5, 0.5])
        std = torch.tensor([0.5, 0.5, 0.5])
        x.sub_(mean[None, :, None, None]).div_(std[None, :, None, None])

        return DPTDepthModel.forward(self, x)


def run(model_path, out_path, is_hybrid):
    """Run MonoDepthNN to compute depth maps.

    Args:
        model_path (str): path to saved model
    """
    print("initialize")

    # load network

    if is_hybrid:
        model = DPTDepthModel_pp(
            path=model_path,
            backbone="vitb_rn50_384",
            non_negative=True,
        )
    else: #large
        model = DPTDepthModel_pp(
            path=model_path,
            backbone="vitl16_384",
            non_negative=True,
        )

    model.eval()
    
    print("start processing")

    # input
    img_input = np.zeros((3, 384, 384), np.float32)  

    # compute
    with torch.no_grad():
        sample = torch.from_numpy(img_input).unsqueeze(0)
        prediction = model.forward(sample)
        prediction = (
            torch.nn.functional.interpolate(
                prediction.unsqueeze(1),
                size=img_input.shape[:2],
                mode="bicubic",
                align_corners=False,
            )
            .squeeze()
            .cpu()
            .numpy()
        )

    torch.onnx.export(model, sample, out_path, opset_version=11)    
    
    print("finished")


if __name__ == "__main__":
    # set paths
    # MODEL_PATH = "model.pt"

    hybrid_path = "../weights/dpt_hybrid-midas-501f0c75.pt"
    large_path = "../weights/dpt_large-midas-2f21e586.pt"

    hybrid_out = os.path.basename(hybrid_path).replace(".pt", "-ALTERED.onnx")
    large_out = os.path.basename(large_path).replace(".pt", "-ALTERED.onnx")
    
    # compute depth maps
    run(hybrid_path, hybrid_out, is_hybrid=True)
    run(large_path, large_out, is_hybrid=False)