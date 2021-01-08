/*
 Copyright (c) 2013 yvt

 This file is part of OpenSpades.

 OpenSpades is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 OpenSpades is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with OpenSpades.  If not, see <http://www.gnu.org/licenses/>.

 */


uniform sampler2D mainTexture;

varying vec2 texCoord;

uniform float enhancement;
uniform float saturation;
uniform vec3 tint;
uniform float sharpening;
uniform float sharpeningFinalGain;

vec3 acesToneMapping(vec3 x)
{
	return clamp((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14), 0.0, 1.0);
}

// 1/(dacesToneMapping(x)/dx)
float acesToneMappingDiffRcp(float color) {
	float denom = 0.0576132 + color * (0.242798 + color);
	return (denom * denom) / (0.0007112 + color * (0.11902 + color * 0.238446));
}

void main() {
	// Input is in the device color space
	gl_FragColor = texture2D(mainTexture, texCoord);

	// Blur kernel:  1/4 2/4 1/4
	//               2/4 4/4 2/4
	//               1/4 2/4 1/4
	float dx = dFdx(texCoord.x) * 0.5, dy = dFdy(texCoord.y) * 0.5;
	vec4 blurred = 0.25 * (
		texture2D(mainTexture, texCoord + vec2(dx, dy)) +
		texture2D(mainTexture, texCoord + vec2(dx, -dy)) +
		texture2D(mainTexture, texCoord + vec2(-dx, dy)) +
		texture2D(mainTexture, texCoord + vec2(-dx, -dy)));

	// `sharpening` tells to what extent we must enhance the edges based on
	// global factors.
	float enhancingFactor = sharpening;
#if USE_HDR
	// Now we take the derivative of `acesToneMapping` into consideration.
	// Specifially, when `acesToneMapping` reduces the color contrast
	// around the current pixel by N times, we compensate by scaling
	// `enhancingFactor` by N.
	float localLuminance = dot(blurred.xyz, vec3(1. / 3.));
	float localLuminanceLinear = clamp(localLuminance * localLuminance, 0.0, 1.0);
	enhancingFactor *= acesToneMappingDiffRcp(localLuminanceLinear * 0.8);

	// We don't want specular highlights to cause black edges, so weaken the
	// effect if the local luminance is high.
	localLuminance = max(localLuminance, dot(gl_FragColor.xyz, vec3(1. / 3.)));
	if (localLuminance > 1.0) {
		localLuminance -= 1.0;
		enhancingFactor *= 1.0 - (localLuminance + localLuminance * localLuminance) * 100.0;
	}
#endif

	// Clamp the sharpening effect's intensity.
	enhancingFactor = clamp(enhancingFactor, 1.0, 4.0);

	// Derive the value of `localSharpening` that achieves the desired
	// contrast enhancement. When `sharpeningFinalGain = 2`, the sharpening
	// effect multiplies the color contrast exactly by `enhancingFactor`.
	float localSharpening = (enhancingFactor - 1.0) * sharpeningFinalGain;

	// Given a parameter value `localSharpening`, the sharpening kernel defined
	// in here enhances the color difference across a horizontal or vertical
	// edge by the following factor:
	//
	//    r_sharp = 1 + localSharpening / 2

	// Sharpening is done by reversing the effect of the blur kernel.
	// Clamp the lower bound to suppress the black edges around specular highlights.
	vec3 lowerBound = gl_FragColor.xyz * 0.6;
	gl_FragColor.xyz += (gl_FragColor.xyz - blurred.xyz) * localSharpening;
	gl_FragColor.xyz = max(gl_FragColor.xyz, lowerBound);

	// Apply tinting and manual exposure
	gl_FragColor.xyz *= tint;

	vec3 gray = vec3(dot(gl_FragColor.xyz, vec3(1. / 3.)));
	gl_FragColor.xyz = mix(gray, gl_FragColor.xyz, saturation);

#if USE_HDR
	gl_FragColor.xyz *= gl_FragColor.xyz; // linearize
	gl_FragColor.xyz = acesToneMapping(gl_FragColor.xyz * 0.8);
	gl_FragColor.xyz = sqrt(gl_FragColor.xyz); // delinearize
	gl_FragColor.xyz = mix(gl_FragColor.xyz,
						   smoothstep(0., 1., gl_FragColor.xyz),
						   enhancement);
#else
	gl_FragColor.xyz = mix(gl_FragColor.xyz,
						   smoothstep(0., 1., gl_FragColor.xyz),
						   enhancement);
#endif

	gl_FragColor.w = 1.;

}

