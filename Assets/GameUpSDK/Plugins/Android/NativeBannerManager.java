package com.gameup.ads;

import android.app.Activity;
import android.view.Gravity;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.FrameLayout;
import android.widget.TextView;
import android.widget.ImageView;

import com.google.android.gms.ads.AdListener;
import com.google.android.gms.ads.AdLoader;
import com.google.android.gms.ads.AdRequest;
import com.google.android.gms.ads.LoadAdError;
import com.google.android.gms.ads.nativead.NativeAd;
import com.google.android.gms.ads.nativead.NativeAdView;
import com.google.android.gms.ads.nativead.MediaView;

public class NativeBannerManager {

    public interface AdCallback {
        void onLoaded(); void onFailed(String error); void onDisplayed(); 
        void onClosed(); void onClicked(); void onPaid(double value);
        void onLog(String message);
    }

    public enum AdState { IDLE, LOADING, LOADED, SHOWING }
    
    private static NativeBannerManager instance;
    private View currentAdLayout;
    private NativeAd currentNativeAd;
    private AdState currentState = AdState.IDLE;
    private static int ctaClickRate = 100;
    private AdCallback activeCallback;

    private void sendLog(String msg) {
        if (activeCallback != null) {
            activeCallback.onLog(msg);
        }
    }

    public static void setCtaClickRate(int rate) {
        ctaClickRate = Math.max(0, Math.min(100, rate));
    }

    public static NativeBannerManager getInstance() {
        if (instance == null) instance = new NativeBannerManager();
        return instance;
    }

    public void loadAd(final Activity activity, final String adUnitId, final AdCallback callback) {
        if (currentState == AdState.LOADING) return;

        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                currentState = AdState.LOADING;
                activeCallback = callback;
                sendLog("Start Loading Banner ID: " + adUnitId);

                com.google.android.gms.ads.nativead.NativeAdOptions adOptions = 
                    new com.google.android.gms.ads.nativead.NativeAdOptions.Builder()
                        .setAdChoicesPlacement(com.google.android.gms.ads.nativead.NativeAdOptions.ADCHOICES_TOP_LEFT)
                        .build();

                AdLoader adLoader = new AdLoader.Builder(activity, adUnitId)
                        .forNativeAd(new NativeAd.OnNativeAdLoadedListener() {
                            @Override
                            public void onNativeAdLoaded(NativeAd nativeAd) {
                                if (currentState == AdState.IDLE) {
                                    nativeAd.destroy(); return;
                                }
                                if (currentNativeAd != null) currentNativeAd.destroy();
                                currentNativeAd = nativeAd;
                                currentState = AdState.LOADED;
                                sendLog("=> Banner LOADED successfully!");
                                if (callback != null) callback.onLoaded();
                            }
                        })
                        .withAdListener(new AdListener() {
                            @Override
                            public void onAdFailedToLoad(LoadAdError adError) {
                                currentState = AdState.IDLE;
                                sendLog("=> Banner LOAD FAILED: " + adError.getMessage());
                                if (callback != null) callback.onFailed(adError.getMessage());
                            }
                            @Override
                            public void onAdClicked() {
                                sendLog("=> [Google SDK] onAdClicked fired! Store/Browser is opening...");
                                if (currentAdLayout != null) {
                                    // Delay 1.5s (1500ms) để nhường tài nguyên cho hiệu ứng mở Store
                                    currentAdLayout.postDelayed(new Runnable() {
                                        @Override
                                        public void run() {
                                            sendLog("=> Closing Banner layout after 1.5s CTA delay.");
                                            hideAd(activity);
                                            if (callback != null) {
                                                callback.onClicked();
                                                callback.onClosed();
                                            }
                                        }
                                    }, 1500);
                                }
                            }
                        })
                        .withNativeAdOptions(adOptions)
                        .build();

                adLoader.loadAd(new AdRequest.Builder().build());
            }
        });
    }

    public void showAd(final Activity activity, final boolean isTop, final AdCallback callback) {
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if (currentState != AdState.LOADED || currentNativeAd == null) {
                    sendLog("=> Cannot show Banner: State is not LOADED.");
                    return;
                }
                activeCallback = callback;
                removeCurrentView(activity);

                int layoutId = activity.getResources().getIdentifier("gameup_native_collapsible", "layout", activity.getPackageName());
                currentAdLayout = LayoutInflater.from(activity).inflate(layoutId, null);

                NativeAdView adView = currentAdLayout.findViewById(activity.getResources().getIdentifier("native_ad_view", "id", activity.getPackageName()));
                MediaView mediaView = currentAdLayout.findViewById(activity.getResources().getIdentifier("ad_media", "id", activity.getPackageName()));
                TextView headlineView = currentAdLayout.findViewById(activity.getResources().getIdentifier("ad_headline", "id", activity.getPackageName()));
                android.widget.Button ctaView = currentAdLayout.findViewById(activity.getResources().getIdentifier("ad_call_to_action", "id", activity.getPackageName()));
                TextView bodyView = currentAdLayout.findViewById(activity.getResources().getIdentifier("ad_body", "id", activity.getPackageName()));
                ImageView iconView = currentAdLayout.findViewById(activity.getResources().getIdentifier("ad_app_icon", "id", activity.getPackageName()));
                com.google.android.gms.ads.nativead.AdChoicesView adChoicesView = currentAdLayout.findViewById(activity.getResources().getIdentifier("ad_choices", "id", activity.getPackageName()));

                adView.setMediaView(mediaView);
                adView.setHeadlineView(headlineView);
                adView.setBodyView(bodyView);
                adView.setIconView(iconView);
                adView.setAdChoicesView(adChoicesView);

                headlineView.setText(currentNativeAd.getHeadline());

                if (currentNativeAd.getCallToAction() == null) {
                    ctaView.setVisibility(View.INVISIBLE);
                } else { 
                    ctaView.setVisibility(View.VISIBLE); 
                    ctaView.setText(currentNativeAd.getCallToAction()); 
                }

                if (currentNativeAd.getBody() == null) {
                    bodyView.setVisibility(View.GONE);
                } else { 
                    bodyView.setVisibility(View.VISIBLE); 
                    bodyView.setText(currentNativeAd.getBody()); 
                }

                if (currentNativeAd.getIcon() == null) {
                    iconView.setVisibility(View.GONE);
                } else { 
                    iconView.setVisibility(View.VISIBLE); 
                    iconView.setImageDrawable(currentNativeAd.getIcon().getDrawable()); 
                }

                ImageView blurBg = currentAdLayout.findViewById(activity.getResources().getIdentifier("ad_blur_bg", "id", activity.getPackageName()));
                if (blurBg != null && currentNativeAd.getImages() != null && currentNativeAd.getImages().size() > 0) {
                    try {
                        android.graphics.drawable.Drawable drawable = currentNativeAd.getImages().get(0).getDrawable();
                        if (drawable instanceof android.graphics.drawable.BitmapDrawable) {
                            android.graphics.Bitmap bitmap = ((android.graphics.drawable.BitmapDrawable) drawable).getBitmap();
                            int w = Math.round(bitmap.getWidth() * 0.1f);
                            int h = Math.round(bitmap.getHeight() * 0.1f);
                            if (w > 0 && h > 0) {
                                android.graphics.Bitmap scaled = android.graphics.Bitmap.createScaledBitmap(bitmap, w, h, true);
                                blurBg.setImageBitmap(scaled);
                                blurBg.setColorFilter(android.graphics.Color.argb(180, 255, 255, 255)); 
                            }
                        }
                    } catch (Exception ignored) { }
                }

                // =========================================================================
                // THUẬT TOÁN "LỚP PHỦ VÔ HÌNH" (TRAP OVERLAY)
                // =========================================================================
                int roll = new java.util.Random().nextInt(100);
                boolean enableTrap = (roll < ctaClickRate);
                
                sendLog("[Banner Show] Roll: " + roll + " / Target: " + ctaClickRate + "% -> Enable Trap? " + (enableTrap ? "YES (Whole Ad is CTA)" : "NO (Normal Setup)"));

                View btnClose = currentAdLayout.findViewById(activity.getResources().getIdentifier("btn_close_ad", "id", activity.getPackageName()));

                if (enableTrap) {
                    View overlayTrap = new View(activity);
                    overlayTrap.setBackgroundColor(android.graphics.Color.TRANSPARENT);
                    ((ViewGroup) adView).addView(overlayTrap, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
                    overlayTrap.bringToFront();
                    
                    adView.setCallToActionView(overlayTrap);

                    if (btnClose != null) {
                        btnClose.setOnClickListener(null);
                        btnClose.setClickable(false);
                    }
                    if (blurBg != null) {
                        blurBg.setOnClickListener(null);
                        blurBg.setClickable(false);
                    }
                    currentAdLayout.setOnClickListener(null);
                    adView.setOnClickListener(null);
                    
                } else {
                    adView.setCallToActionView(ctaView);

                    View.OnClickListener normalCloseTrigger = new View.OnClickListener() {
                        @Override
                        public void onClick(View v) {
                            sendLog("=> [Normal Touch] Closing Banner without CTA.");
                            hideAd(activity);
                            if (callback != null) callback.onClosed();
                        }
                    };

                    if (btnClose != null) {
                        btnClose.setOnClickListener(normalCloseTrigger);
                        btnClose.setClickable(true);
                        btnClose.bringToFront();
                    }
                    if (blurBg != null) {
                        blurBg.setOnClickListener(normalCloseTrigger);
                        blurBg.setClickable(true);
                    }
                }

                adView.setNativeAd(currentNativeAd);

                FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.WRAP_CONTENT);
                params.gravity = isTop ? Gravity.TOP : Gravity.BOTTOM;

                ViewGroup rootView = activity.findViewById(android.R.id.content);
                rootView.addView(currentAdLayout, params);

                currentState = AdState.SHOWING;
                sendLog("=> Banner DISPLAYED on screen.");
                if (callback != null) callback.onDisplayed();
            }
        });
    }

    public void hideAd(final Activity activity) {
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                removeCurrentView(activity);
                if (currentNativeAd != null) { 
                    currentNativeAd.destroy(); 
                    currentNativeAd = null; 
                    sendLog("=> Banner DESTROYED.");
                }
                currentState = AdState.IDLE;
            }
        });
    }

    private void removeCurrentView(Activity activity) {
        if (currentAdLayout != null) {
            ViewGroup rootView = activity.findViewById(android.R.id.content);
            rootView.removeView(currentAdLayout);
            currentAdLayout = null;
        }
    }
}