#version 450

// each model
layout(push_constant) uniform ModelInfo { mat4 model; }
m;

// each frame
layout(binding = 0) uniform WorldInfo {
  mat4 view;
  mat4 proj;
}
w;

layout(location = 0) in vec3 aPos;
layout(location = 1) in vec4 aColor;

layout(location = 0) out vec4 vColor;

void main() {
  gl_Position = w.proj * w.view * m.model * vec4(aPos, 1.0);
  vColor = aColor;
}
