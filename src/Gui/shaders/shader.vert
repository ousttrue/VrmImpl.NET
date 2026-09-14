#version 450

layout(binding=0)uniform UniformBufferObject{
  mat4 model;
  mat4 view;
  mat4 projection;
}ubo;

layout(location=0)in vec3 aPos;
layout(location=1)in vec3 aNormal;
layout(location=2)in vec2 aTexCoords;

layout(location=0)out vec3 fPos;
layout(location=1)out vec3 fNormal;
layout(location=2)out vec2 fTexCoords;

void main(){
  gl_Position=ubo.projection*ubo.view*ubo.model*vec4(aPos,1.);
  fPos=(ubo.model*vec4(aPos,1.)).xyz;
  fNormal=mat3(transpose(inverse(ubo.model)))*aNormal;
  fTexCoords=aTexCoords;
}
