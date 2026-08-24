package com.navalcommand.profile;

import android.app.Activity;
import android.app.Fragment;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.net.Uri;
import android.os.Bundle;
import android.util.Base64;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.lang.reflect.Method;

public final class NavalImagePicker {
    private static final String TAG = "NavalProfileImagePicker";

    private NavalImagePicker() { }

    public static void open(final Activity activity, final String gameObjectName) {
        if (activity == null) return;
        activity.runOnUiThread(() -> {
            PickerFragment fragment = new PickerFragment();
            Bundle arguments = new Bundle();
            arguments.putString("gameObjectName", gameObjectName);
            fragment.setArguments(arguments);
            activity.getFragmentManager().beginTransaction().add(fragment, TAG).commitAllowingStateLoss();
        });
    }

    public static final class PickerFragment extends Fragment {
        private static final int REQUEST_IMAGE = 24519;
        private String receiver;

        @Override
        public void onCreate(Bundle state) {
            super.onCreate(state);
            receiver = getArguments().getString("gameObjectName", "NavalGameController");
            Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
            intent.addCategory(Intent.CATEGORY_OPENABLE);
            intent.setType("image/*");
            startActivityForResult(intent, REQUEST_IMAGE);
        }

        @Override
        public void onActivityResult(int requestCode, int resultCode, Intent data) {
            super.onActivityResult(requestCode, resultCode, data);
            if (requestCode == REQUEST_IMAGE && resultCode == Activity.RESULT_OK && data != null) {
                encodeAndSend(data.getData());
            } else if (requestCode == REQUEST_IMAGE && resultCode != Activity.RESULT_CANCELED) {
                sendToUnity(receiver, "OnProfileImagePickerError", "IMAGE_PICK_FAILED");
            }
            if (getActivity() != null)
                getActivity().getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss();
        }

        private void encodeAndSend(Uri uri) {
            try (InputStream stream = getActivity().getContentResolver().openInputStream(uri)) {
                Bitmap source = BitmapFactory.decodeStream(stream);
                if (source == null) throw new IllegalArgumentException("IMAGE_DECODE_FAILED");
                int side = Math.min(source.getWidth(), source.getHeight());
                int x = (source.getWidth() - side) / 2;
                int y = (source.getHeight() - side) / 2;
                Bitmap crop = Bitmap.createBitmap(source, x, y, side, side);
                Bitmap scaled = Bitmap.createScaledBitmap(crop, 256, 256, true);
                ByteArrayOutputStream bytes = new ByteArrayOutputStream();
                scaled.compress(Bitmap.CompressFormat.JPEG, 78, bytes);
                String encoded = Base64.encodeToString(bytes.toByteArray(), Base64.NO_WRAP);
                sendToUnity(receiver, "OnProfileImagePicked", encoded);
                if (scaled != crop) scaled.recycle();
                if (crop != source) crop.recycle();
                source.recycle();
            } catch (Exception exception) {
                sendToUnity(receiver, "OnProfileImagePickerError", exception.getMessage());
            }
        }
    }

    private static void sendToUnity(String receiver, String methodName, String value) {
        try {
            Class<?> unityPlayer = Class.forName("com.unity3d.player.UnityPlayer");
            Method sendMessage = unityPlayer.getMethod(
                "UnitySendMessage", String.class, String.class, String.class);
            sendMessage.invoke(null, receiver, methodName, value == null ? "" : value);
        } catch (Exception ignored) { }
    }
}
