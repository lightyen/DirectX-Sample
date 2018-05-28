#pragma once

#include <fbxsdk.h>
// http://www.arkaistudio.com/blog/1078/unity/%E4%B8%80%E8%B5%B7%E5%AD%B8-unity-shader-%E4%B8%80%EF%BC%9A%E6%96%B0%E6%89%8B%E5%85%A5%E9%96%80
// http://help.autodesk.com/view/FBX/2018/ENU/?guid=FBX_Developer_Help_importing_and_exporting_a_scene_importing_a_scene_html

namespace MyGame {

	class MyMesh {
	public:
		ID3D11Buffer* VertexBuffer;
		ID3D11Buffer* IndexBuffer;
		int indexCount;
		~MyMesh() {
			if (VertexBuffer) {
				VertexBuffer->Release();
			}

			if (IndexBuffer) {
				IndexBuffer->Release();
			}
		}
	};

	class MyNode {
	public:
		MyMesh* Mesh;
		String Name;
		vector<MyNode*> Children;
		Matrix GlobalTransfrom;
		~MyNode() {
			if (Children.size()) {
				for (size_t i = 0; i < Children.size(); i++) {
					delete Children[i];
				}
			}
			if (Mesh) delete Mesh;
		}
	};

	class MyCamera {
	public:
		Vector3 Position;
		Vector3 Target;
		Vector3 Up;
		float NearPlane;
		float FarPlane;
		float FilmWidth;
		float FilmHeight;
	};

	class MyScene {
	public:
		MyNode* Root;
		MyCamera* Camera;
		FbxManager* fbxManager;
		FbxScene* fbxScene;
	public:
		MyScene(FbxManager* fbxManager, FbxScene* fbxScene) {
			this->fbxManager = fbxManager;
			this->fbxScene = fbxScene;
			Root = nullptr;
			Camera = nullptr;
		}

		bool CreateBuffer(ID3D11Device* device) {
			FbxNode* root = fbxScene->GetRootNode();
			if (fbxScene->GetRootNode() != nullptr && device != nullptr) {
				Root = new MyNode();
				Root->Name = TEXT("");
				Root->Mesh = nullptr;

				FbxAMatrix& transform = root->EvaluateGlobalTransform();

				for (int i = 0; i < 4; i++) {
					for (int j = 0; j < 4; j++) {
						Root->GlobalTransfrom.m[i][j] = (float)transform.Get(i, j);
					}
				}

				for (int i = 0; i < root->GetChildCount(); i++) {
					FbxNode* child = root->GetChild(i);
					if (CreateBuffer(device, child, Root) == false) return false;
				}
				return true;
			}
			return false;
		}

		~MyScene() {
			if (Root) delete Root;
			if (Camera) delete Camera;
		}

	private:
		bool CreateBuffer(ID3D11Device* device, FbxNode* node, MyNode* parentNode) {
			if (node != nullptr && device != nullptr) {

				MyNode* n = new MyNode();
				
				int count = node->GetChildCount();
				const char* name = node->GetName();
				n->Name = String(strlen(name));
				MultiByteToWideChar(CP_ACP, MB_PRECOMPOSED, name, -1, (wchar_t*)n->Name, n->Name.Length());;
				parentNode->Children.push_back(n);

				FbxAMatrix& transform = node->EvaluateGlobalTransform();
				
				for (int i = 0; i < 4; i++) {
					for (int j = 0; j < 4; j++) {
						n->GlobalTransfrom.m[i][j] = (float)transform.Get(i, j);
					}
				}
				FbxNodeAttribute* NodeAttribute = node->GetNodeAttribute();
				FbxNodeAttribute::EType AttributeType = NodeAttribute->GetAttributeType();
				if (AttributeType == FbxNodeAttribute::eMesh) {

					n->Mesh = new MyMesh();
					FbxMesh* mesh = (FbxMesh*)node->GetNodeAttribute();

					FbxVector4* fbxVertices = mesh->GetControlPoints();
					int controlPointsCount = mesh->GetControlPointsCount();
					int cPolygonCount = mesh->GetPolygonCount();

					vector<SimpleVertex> vertices;
					vector<int> indices;

					for (int j = 0; j < controlPointsCount; j++) {
						SimpleVertex v;
						v.Position = XMFLOAT4(
							(float)fbxVertices[j].mData[0],
							(float)fbxVertices[j].mData[1],
							(float)fbxVertices[j].mData[2],
							1.0f);

						vertices.push_back(v);
					}

					FbxStringList UVSetNames;
					mesh->GetUVSetNames(UVSetNames);
					
					for (int j = 0; j < UVSetNames.GetCount(); j++) {
						auto str = UVSetNames[j];
						String kkk = String(strlen(str));
						MultiByteToWideChar(CP_ACP, MB_PRECOMPOSED, name, -1, (wchar_t*)kkk, kkk.Length());;
					}

					

					for (int j = 0; j < cPolygonCount; j++) {
						int iNumVertices = mesh->GetPolygonSize(j);
						
						for (int k = 0; k < iNumVertices; k++) {
							int iControlPointIndex = mesh->GetPolygonVertex(j, k);
							bool Unmapped;
							FbxVector2 uv;
							
							if (UVSetNames.GetCount() > 0) {
								if (mesh->GetPolygonVertexUV(j, k, UVSetNames[0], uv, Unmapped)) {
									vertices[iControlPointIndex].TexCoord = XMFLOAT2(uv.mData[0], uv.mData[1]);
								}
							}

							indices.push_back(iControlPointIndex);
						}
					}

					n->Mesh->indexCount = (int)indices.size();

					// 建立模型頂點緩衝區
					HRESULT hr;
					D3D11_BUFFER_DESC bd;
					ZeroMemory(&bd, sizeof(bd));
					bd.Usage = D3D11_USAGE_DEFAULT;
					bd.ByteWidth = vertices.size() * sizeof(SimpleVertex);
					bd.BindFlags = D3D11_BIND_VERTEX_BUFFER;
					bd.CPUAccessFlags = 0;
					D3D11_SUBRESOURCE_DATA srd;
					ZeroMemory(&srd, sizeof(srd));
					srd.pSysMem = vertices.data();
					hr = device->CreateBuffer(&bd, &srd, &n->Mesh->VertexBuffer);
					if (!SUCCEEDED(hr)) {
						return false;
					}

					// 建立索引緩衝區
					D3D11_BUFFER_DESC indexDesc;
					ZeroMemory(&indexDesc, sizeof(indexDesc));
					indexDesc.ByteWidth = indices.size() * sizeof(int);
					indexDesc.BindFlags = D3D11_BIND_INDEX_BUFFER;
					indexDesc.Usage = D3D11_USAGE_DEFAULT;
					indexDesc.CPUAccessFlags = 0;
					D3D11_SUBRESOURCE_DATA indexsrd;
					ZeroMemory(&indexsrd, sizeof(indexsrd));
					indexsrd.pSysMem = indices.data();
					hr = device->CreateBuffer(&indexDesc, &indexsrd, &n->Mesh->IndexBuffer);
					if (!SUCCEEDED(hr)) {
						return false;
					}

				} else if (AttributeType == FbxNodeAttribute::eLight) {

				} else if (AttributeType == FbxNodeAttribute::eCamera) {
					if (Camera != NULL) {
						FbxCamera* camera = (FbxCamera*)node->GetNodeAttribute();
						auto position = camera->EvaluatePosition();
						auto target = camera->EvaluateLookAtPosition();
						auto up = camera->UpVector.Get();
						Camera = new MyCamera();
						Camera->Position = Vector3(position[0], position[1], position[2]);
						Camera->Target = Vector3(target[0], target[1], target[2]);
						Camera->Up = Vector3(up[0], up[1], up[2]);
						Camera->NearPlane = camera->GetNearPlane();
						Camera->FarPlane = camera->GetFarPlane();
						Camera->FilmWidth = camera->GetApertureWidth();
						Camera->FilmHeight = camera->GetApertureHeight();
					}

				} else if (AttributeType == FbxNodeAttribute::eNull && count > 0) {
					for (int i = 0; i < count; i++) {
						FbxNode* child = node->GetChild(i);
						if (CreateBuffer(device, child, n) == false) return false;
					}
				}
				return true;
			}
			return false;
		}
	};


}