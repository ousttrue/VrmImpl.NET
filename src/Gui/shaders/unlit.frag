#version 450

layout(binding=1)uniform sampler2D uTexture;

layout(location=0)in vec2 fTexCoords;

layout(location=0)out vec4 oColor;

void main(){
  oColor=texture(uTexture,fTexCoords);
  // oColor = vec4(fTexCoords, 0, 1);
}
