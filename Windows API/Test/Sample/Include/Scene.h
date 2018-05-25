#pragma once

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
		Matrix LocalTransfrom;
		~MyNode() {
			if (Children.size()) {
				for (size_t i = 0; i < Children.size(); i++) {
					delete Children[i];
				}
			}
			if (Mesh) delete Mesh;
		}
	};

	class MyScene {
	public:
		MyNode* Root;

	public:
		MyScene() {
			Root = nullptr;
		}

		bool CreateBuffer(ID3D11Device* device, FbxNode* root) {
			if (root != nullptr && device != nullptr) {
				Root = new MyNode();
				Root->Name = TEXT("");
				Root->Mesh = nullptr;

				FbxAMatrix& transform = root->EvaluateLocalTransform();

				for (int i = 0; i < 4; i++) {
					for (int j = 0; j < 4; j++) {
						Root->LocalTransfrom.m[i][j] = (float)transform.Get(i, j);
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

				FbxAMatrix& transform = node->EvaluateLocalTransform();

				for (int i = 0; i < 4; i++) {
					for (int j = 0; j < 4; j++) {
						n->LocalTransfrom.m[i][j] = (float)transform.Get(i, j);
					}
				}

				FbxNodeAttribute::EType AttributeType = node->GetNodeAttribute()->GetAttributeType();
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

					for (int j = 0; j < cPolygonCount; j++) {
						int iNumVertices = mesh->GetPolygonSize(j);
						for (int k = 0; k < iNumVertices; k++) {
							int iControlPointIndex = mesh->GetPolygonVertex(j, k);
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

				} else if (count > 0) {
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